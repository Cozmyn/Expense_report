/**
 * Crash-safe, client-side encrypted recovery of pending receipt drafts.
 * Browser IndexedDB holds a non-extractable AES-GCM CryptoKey and ciphertext;
 * financial fields are never placed in localStorage or sessionStorage.
 * This protects at rest against accidental local inspection, NOT XSS or
 * someone with access to an already-unlocked browser profile.
 */
export const DRAFT_RECOVERY_DAYS = 7;
const DATABASE = 'decont-encrypted-receipt-recovery';
const TABS = 'decont-draft-tab';
const keyId = 'v1';

function currentTab() {
  try {
    const old = sessionStorage.getItem(TABS);
    if (old && /^[0-9a-f-]{36}$/i.test(old)) return old;
    const next = crypto.randomUUID();
    sessionStorage.setItem(TABS, next);
    return next;
  } catch {
    // When sessionStorage is disabled, each document can still be recovered by
    // the scope index; it simply receives a new tab-specific backup.
    return crypto.randomUUID();
  }
}
const tabId = currentTab();

function reqValue(request) {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error || new Error('Encrypted draft storage failed'));
  });
}

function txDone(tx) {
  return new Promise((resolve, reject) => {
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error || new Error('Encrypted draft transaction failed'));
    tx.onabort = () => reject(tx.error || new Error('Encrypted draft transaction aborted'));
  });
}

function openStore() {
  if (!globalThis.indexedDB || !globalThis.crypto?.subtle)
    return Promise.reject(new Error('Secure local draft recovery unavailable in this browser'));
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DATABASE, 1);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains('keys')) db.createObjectStore('keys');
      if (!db.objectStoreNames.contains('drafts')) {
        const drafts = db.createObjectStore('drafts', { keyPath: 'id' });
        drafts.createIndex('scope', 'scope', { unique: false });
      }
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error || new Error('Cannot open private draft database'));
    req.onblocked = () => reject(new Error('Close another tab that is upgrading draft storage'));
  });
}

async function masterKey(db) {
  // Create the candidate outside the IDB transaction. Its read+put are in
  // ONE readwrite transaction to prevent two tabs from rotating the same key.
  const candidate = await crypto.subtle.generateKey(
    { name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
  return new Promise((resolve, reject) => {
    const tx = db.transaction('keys', 'readwrite');
    const store = tx.objectStore('keys');
    let key = null;
    store.get(keyId).onsuccess = e => {
      key = e.target.result || candidate;
      if (!e.target.result) store.put(candidate, keyId);
    };
    tx.oncomplete = () => resolve(key);
    tx.onerror = () => reject(tx.error || new Error('Cannot read encrypted draft key'));
    tx.onabort = () => reject(tx.error || new Error('Encrypted draft key transaction aborted'));
  });
}

export async function encryptDraftSnapshot(snapshot, key) {
  const plain = new TextEncoder().encode(JSON.stringify(snapshot));
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const cipher = new Uint8Array(await crypto.subtle.encrypt(
    { name: 'AES-GCM', iv }, key, plain));
  const hash = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', plain)))
    .map(x => x.toString(16).padStart(2, '0')).join('');
  return { iv, cipher, hash };
}
export async function decryptDraftSnapshot(record, key) {
  const plain = await crypto.subtle.decrypt(
    { name: 'AES-GCM', iv: new Uint8Array(record.iv) }, key,
    new Uint8Array(record.cipher));
  return JSON.parse(new TextDecoder().decode(plain));
}
async function snapshotHash(snapshot) {
  const bytes = new TextEncoder().encode(JSON.stringify(snapshot));
  return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', bytes)))
    .map(x => x.toString(16).padStart(2, '0')).join('');
}

export async function stageEncryptedDraft(scope, snapshot) {
  if (!scope || typeof scope !== 'string') throw new Error('Private draft scope is required');
  const db = await openStore();
  try {
    const key = await masterKey(db);
    const encrypted = await encryptDraftSnapshot(snapshot, key);
    const now = Date.now();
    const tx = db.transaction('drafts', 'readwrite');
    tx.objectStore('drafts').put({
      id: scope + '::' + tabId, scope, tabId,
      iv: encrypted.iv, cipher: encrypted.cipher, hash: encrypted.hash,
      updatedAt: now, expiresAt: now + DRAFT_RECOVERY_DAYS * 86400000,
    });
    await txDone(tx);
    return encrypted.hash;
  } finally { db.close(); }
}

export async function recoverEncryptedDrafts(scope) {
  const db = await openStore();
  try {
    const key = await masterKey(db);
    const tx = db.transaction('drafts', 'readwrite');
    const completed = txDone(tx);
    const store = tx.objectStore('drafts');
    const records = await new Promise((resolve, reject) => {
      const request = store.index('scope').getAll(scope);
      request.onsuccess = () => {
        const rows = request.result;
        const now = Date.now();
        for (const row of rows)
          if (row.expiresAt <= now) store.delete(row.id);
        resolve(rows);
      };
      request.onerror = () => reject(request.error || new Error('Cannot read encrypted drafts'));
    });
    await completed;
    const now = Date.now();
    const valid = records.filter(x => x.expiresAt > now);
    const result = [];
    for (const record of valid) {
      result.push({ id: record.id, updatedAt: record.updatedAt,
        ownTab: record.tabId === tabId,
        snapshot: await decryptDraftSnapshot(record, key) });
    }
    return result.sort((a, b) => b.updatedAt - a.updatedAt);
  } finally { db.close(); }
}

export async function clearOwnEncryptedDraftIfMatch(scope, snapshot) {
  const hash = await snapshotHash(snapshot);
  const db = await openStore();
  try {
    return await new Promise((resolve, reject) => {
      const tx = db.transaction('drafts', 'readwrite');
      const store = tx.objectStore('drafts');
      let removed = false;
      store.get(scope + '::' + tabId).onsuccess = e => {
        const prior = e.target.result;
        if (prior?.hash === hash) {
          store.delete(prior.id);
          removed = true;
        }
      };
      tx.oncomplete = () => resolve(removed);
      tx.onerror = () => reject(tx.error || new Error('Encrypted draft cleanup failed'));
      tx.onabort = () => reject(tx.error || new Error('Encrypted draft cleanup aborted'));
    });
  } finally { db.close(); }
}

export async function discardEncryptedDraft(id) {
  if (!id || typeof id !== 'string') throw new Error('Draft ID required');
  const db = await openStore();
  try {
    const tx = db.transaction('drafts', 'readwrite');
    tx.objectStore('drafts').delete(id);
    await txDone(tx);
  } finally { db.close(); }
}
