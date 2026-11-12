import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../src/encryptedDraft.mjs', import.meta.url), 'utf8');
const scoped = 'browser-crypto-recovery-e2e-' + Date.now();

test('IndexedDB encrypted draft survives reload, protects bytes and does not erase newer edits', async ({ page }) => {
  test.setTimeout(60000);
  // Serve the REAL source as same-origin code to honour the application's CSP,
  // without exposing a test-only endpoint in the release image.
  await page.route('**/test-encrypted-draft.mjs', async route =>
    route.fulfill({ contentType: 'text/javascript', body: source }));
  await page.goto('/');
  const first = await page.evaluate(async (scope) => {
    const mod = await import('/test-encrypted-draft.mjs');
    const snapshot = {
      data: { amount: '183.25', supplier: 'PRIVATE TEST VENDOR', description: 'Confidential draft' },
      taxes: { '21': { base: '151.45', vat: '31.80' } }, sourceVersion: 7,
    };
    await mod.stageEncryptedDraft(scope, snapshot);
    const db: IDBDatabase = await new Promise((resolve, reject) => {
      const req = indexedDB.open('decont-encrypted-receipt-recovery');
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    const records: any[] = await new Promise((resolve, reject) => {
      const req = db.transaction('drafts').objectStore('drafts').getAll();
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    const key: CryptoKey = await new Promise((resolve, reject) => {
      const req = db.transaction('keys').objectStore('keys').get('v1');
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    db.close();
    return {
      storedPlaintext: JSON.stringify(records),
      nonExtractable: key.extractable === false,
      restored: (await mod.recoverEncryptedDrafts(scope))[0].snapshot,
    };
  }, scoped);
  expect(first.nonExtractable).toBe(true);
  expect(first.storedPlaintext).not.toContain('PRIVATE TEST VENDOR');
  expect(first.storedPlaintext).not.toContain('183.25');
  expect(first.restored.data.amount).toBe('183.25');

  await page.reload();
  const recovery = await page.evaluate(async scope => {
    const mod = await import('/test-encrypted-draft.mjs');
    const prior = (await mod.recoverEncryptedDrafts(scope))[0].snapshot;
    const newer = { ...prior, data: { ...prior.data, amount: '184.25' } };
    await mod.stageEncryptedDraft(scope, newer);
    const deletedOld = await mod.clearOwnEncryptedDraftIfMatch(scope, prior);
    const latest = (await mod.recoverEncryptedDrafts(scope))[0].snapshot;
    const deletedLatest = await mod.clearOwnEncryptedDraftIfMatch(scope, newer);
    const remaining = await mod.recoverEncryptedDrafts(scope);
    return { deletedOld, latest, deletedLatest, remaining: remaining.length };
  }, scoped);
  expect(recovery.deletedOld).toBe(false);
  expect(recovery.latest.data.amount).toBe('184.25');
  expect(recovery.deletedLatest).toBe(true);
  expect(recovery.remaining).toBe(0);
});

test('expired encrypted recovery records are removed from disk', async ({ page }) => {
  await page.route('**/test-encrypted-draft.mjs', async route =>
    route.fulfill({ contentType: 'text/javascript', body: source }));
  await page.goto('/');
  const result = await page.evaluate(async scope => {
    const mod = await import('/test-encrypted-draft.mjs');
    await mod.stageEncryptedDraft(scope, { data: { amount: '11.00' }, taxes: {} });
    const db: IDBDatabase = await new Promise((resolve, reject) => {
      const req = indexedDB.open('decont-encrypted-receipt-recovery');
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction('drafts', 'readwrite');
      const store = tx.objectStore('drafts');
      const request = store.index('scope').getAll(scope);
      request.onsuccess = () => {
        for (const item of request.result)
          store.put({ ...item, expiresAt: Date.now() - 1 });
      };
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
    db.close();
    const recovered = await mod.recoverEncryptedDrafts(scope);
    const reopened: IDBDatabase = await new Promise((resolve, reject) => {
      const req = indexedDB.open('decont-encrypted-receipt-recovery');
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    const diskRows: any[] = await new Promise((resolve, reject) => {
      const req = reopened.transaction('drafts').objectStore('drafts').index('scope').getAll(scope);
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    reopened.close();
    return { recovered: recovered.length, diskRows: diskRows.length };
  }, scoped + '-expiry');
  expect(result).toEqual({ recovered: 0, diskRows: 0 });
});
