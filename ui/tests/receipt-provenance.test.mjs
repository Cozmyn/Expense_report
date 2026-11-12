import test from 'node:test';
import assert from 'node:assert/strict';
import { attentionFields, fieldProvenance } from '../src/receiptProvenance.mjs';

test('OCR value is proposed, not verified, and ambiguity is unread', () => {
  assert.equal(fieldProvenance({value:'12.34', source:{value:'12.34', state:'ReviewRequired'}}), 'ocr');
  assert.equal(fieldProvenance({value:'',source:{value:null,state:'Ambiguous'}}), 'unread');
  assert.equal(fieldProvenance({value:'12.34',source:{value:'12.34'},verified:true}), 'verified');
});
test('manual edit cancels verification and calculated values stay distinct', () => {
  assert.equal(fieldProvenance({value:'14',source:{value:'12.34'},changed:true,verified:false}), 'manual');
  assert.equal(fieldProvenance({value:'21.00',calculated:true}), 'calculated');
  assert.equal(fieldProvenance({value:'',source:null}), 'unread');
});
test('attention summary updates when human verifies all required fields', () => {
  const values = {date:'2026-11-12',amount:'',currency:'RON'};
  const sources = {date:{value:'2026-11-12'},amount:{value:null,state:'Ambiguous'},currency:{value:'RON'}};
  assert.deepEqual(attentionFields(values,sources,[],false,['date','amount','currency']),
    ['date','amount','currency']);
  assert.deepEqual(attentionFields({...values,amount:'15.00'},sources,[],true,['date','amount','currency']), []);
  assert.deepEqual(attentionFields(values,sources,['date'],false,['date','amount']),['date','amount']);
});
