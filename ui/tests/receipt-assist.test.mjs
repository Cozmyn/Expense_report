import test from 'node:test';
import assert from 'node:assert/strict';
import { adoptSupplierLookup, offersRegistryName, draftSnapshot, nextReceiptIndex } from '../src/receiptAssist.mjs';

const match = { found: true, valid: true, cui: '18546294', name: 'Official Vendor SRL' };

test('Registry prefill only fills an empty name for the same verified CUI', () => {
  assert.deepEqual(
    adoptSupplierLookup({ cui: 'RO 18546294', supplier: '', supplierConfirmed: true },
      match, '18546294'),
    { cui: 'RO 18546294', supplier: 'Official Vendor SRL', supplierConfirmed: false }
  );
});

test('Registry never overrides a typed supplier or OCR suggestion', () => {
  for (const name of ['Typed Supplier SRL', 'OCR Supplier SRL']) {
    const current = { cui: '18546294', supplier: name, supplierConfirmed: false };
    assert.equal(adoptSupplierLookup(current, match, '18546294'), current);
    assert.equal(offersRegistryName(current, match), true);
  }
});

test('Stale and unvalidated registry replies cannot corrupt another receipt', () => {
  const current = { cui: '12345674', supplier: '', supplierConfirmed: true };
  assert.equal(adoptSupplierLookup(current, match, '18546294'), current);
  assert.equal(adoptSupplierLookup(current, { ...match, valid: false }, current.cui), current);
  assert.equal(adoptSupplierLookup(current, { ...match, cui: current.cui }, '18546294'), current);
  assert.equal(offersRegistryName(current, match), false);
});

test('Missing registry name is not a financial confirmation', () => {
  const current = { cui: '18546294', supplier: '', supplierConfirmed: false };
  assert.equal(adoptSupplierLookup(current, { ...match, name: '' }, current.cui), current);
  assert.equal(adoptSupplierLookup(current, { ...match, found: false }, current.cui), current);
});

test('Draft snapshot preserves the newest receipt, tax rows and OCR version', () => {
  const data = { amount: '123.45', description: 'Hotel' };
  const taxes = { 21: { base: '100', vat: '23.45' } };
  assert.deepEqual(draftSnapshot(data, taxes, 8),
    { data, taxes, sourceVersion: 8 });
});

test('Receipt selector keyboard navigation wraps and supports first/last', () => {
  assert.equal(nextReceiptIndex(0, 4, 'ArrowRight'), 1);
  assert.equal(nextReceiptIndex(0, 4, 'ArrowLeft'), 3);
  assert.equal(nextReceiptIndex(3, 4, 'ArrowRight'), 0);
  assert.equal(nextReceiptIndex(2, 4, 'Home'), 0);
  assert.equal(nextReceiptIndex(1, 4, 'End'), 3);
  assert.equal(nextReceiptIndex(0, 0, 'ArrowRight'), null);
  assert.equal(nextReceiptIndex(0, 4, 'Enter'), null);
});
