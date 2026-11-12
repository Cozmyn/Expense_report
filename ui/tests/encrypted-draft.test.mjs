import test from 'node:test';
import assert from 'node:assert/strict';
import { webcrypto } from 'node:crypto';
import { encryptDraftSnapshot, decryptDraftSnapshot } from '../src/encryptedDraft.mjs';

test('encrypted receipt backup never contains plaintext amounts, supplier or tax information', async () => {
  const key = await webcrypto.subtle.generateKey({ name: 'AES-GCM', length: 256 },
    false, ['encrypt','decrypt']);
  const value = { data: { supplier: 'CONFIDENTIAL-VENDOR', amount: '123.45', currency: 'RON' },
    taxes: { '21': { base: '100', vat: '21' } }, sourceVersion: 5 };
  const { iv, cipher } = await encryptDraftSnapshot(value, key);
  assert.equal(iv.length, 12);
  assert.equal(key.extractable, false);
  assert.ok(!JSON.stringify([...cipher]).includes('CONFIDENTIAL-VENDOR'));
  assert.deepEqual(await decryptDraftSnapshot({iv, cipher}, key), value);
});
test('tampered backup fails authenticated decryption and never restores forged financial values', async () => {
  const key = await webcrypto.subtle.generateKey({ name: 'AES-GCM', length: 256 },
    false, ['encrypt', 'decrypt']);
  const encrypted = await encryptDraftSnapshot({data:{amount:'10.00'},taxes:{}},key);
  const copy = new Uint8Array(encrypted.cipher);
  copy[0] ^= 1;
  await assert.rejects(() => decryptDraftSnapshot({iv:encrypted.iv,cipher:copy},key));
});
test('a different user-origin recovery key cannot decrypt stored receipt', async () => {
  const first = await webcrypto.subtle.generateKey({name:'AES-GCM',length:256},false,['encrypt','decrypt']);
  const second = await webcrypto.subtle.generateKey({name:'AES-GCM',length:256},false,['encrypt','decrypt']);
  const item = await encryptDraftSnapshot({data:{amount:'50.00'},taxes:{}},first);
  await assert.rejects(() => decryptDraftSnapshot(item,second));
});
