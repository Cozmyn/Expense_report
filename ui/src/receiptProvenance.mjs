/**
 * Provenance is advisory; source text and explicit human confirmation
 * remain the authority. Never call an OCR proposal "verified".
 */
export function fieldProvenance({ value, source = null, changed = false, verified = false, calculated = false }) {
  const present = value !== null && value !== undefined && String(value).trim() !== '';
  if (!present) return 'unread';
  if (verified) return 'verified';
  if (changed) return 'manual';
  if (source?.state === 'Ambiguous' || source?.state === 'Missing') return 'unread';
  if (calculated) return 'calculated';
  if (source && source.value !== null && source.value !== undefined
      && String(source.value).trim() !== '') {
    if (!present || String(value).trim() !== String(source.value).trim()) return 'manual';
    return 'ocr';
  }
  return present ? 'manual' : 'unread';
}
export function attentionFields(values, sources, changedFields = [], verified = false, required = []) {
  const changed = new Set(changedFields);
  return required.filter(key => {
    const status = fieldProvenance({
      value: values?.[key], source: sources?.[key],
      changed: changed.has(key), verified,
    });
    return status !== 'verified';
  });
}
