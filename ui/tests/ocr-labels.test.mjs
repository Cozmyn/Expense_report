import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import vm from 'node:vm';
import ts from 'typescript';

// Exercise the actual TypeScript label helper used by both OCR review screens.
const source = readFileSync(new URL('../src/OcrLabels.ts', import.meta.url), 'utf8');
const js = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const module = { exports: {} };
vm.runInNewContext(js, { module, exports: module.exports });
const { ocrFieldLabel } = module.exports;

test('core OCR field labels are understandable in Romanian and English', () => {
  const ro = (romanian) => romanian;
  const en = (_romanian, english) => english;
  const fields = {
    supplier: ['Furnizor', 'Supplier'],
    cui: ['CUI furnizor', 'Supplier VAT ID'],
    invoice: ['Număr document', 'Document number'],
    date: ['Data documentului', 'Document date'],
    currency: ['Monedă', 'Currency'],
    amount: ['Total', 'Total'],
    description: ['Descriere', 'Description'],
    hasCompanyCui: ['CUI-ul companiei pe document', 'Company VAT ID on document'],
  };
  for (const [field, [romanian, english]] of Object.entries(fields)) {
    assert.equal(ocrFieldLabel(field, ro), romanian);
    assert.equal(ocrFieldLabel(field, en), english);
    assert.ok(!english.includes('_'), field + ' should never expose an internal field key');
  }
});

test('unrecognized OCR field is displayed without losing its source key', () => {
  assert.equal(ocrFieldLabel('futureVendorField', (_ro, en) => en), 'futureVendorField');
});
