/**
 * Pure, privacy-safe helpers for a human-reviewed receipt.
 * Supplier registry matches only prefill a BLANK field, never overwrite
 * typed text or another OCR suggestion.
 */
export function normalizedCui(value) {
  return String(value ?? '').trim().replace(/^RO\s*/i, '').replace(/[^\d]/g, '');
}

export function adoptSupplierLookup(current, lookup, requestedCui) {
  if (!lookup?.found || !lookup.valid || !lookup.name || !lookup.cui)
    return current;
  const currentCode = normalizedCui(current.cui);
  if (!currentCode || currentCode !== normalizedCui(requestedCui)
      || currentCode !== normalizedCui(lookup.cui))
    return current;
  if (String(current.supplier ?? '').trim() !== '') return current;
  return { ...current, supplier: lookup.name, supplierConfirmed: false };
}

export function offersRegistryName(current, lookup) {
  return Boolean(lookup?.found && lookup.valid && lookup.name
    && normalizedCui(current.cui) && normalizedCui(current.cui) === normalizedCui(lookup.cui)
    && String(current.supplier ?? '').trim().toLocaleLowerCase() !== lookup.name.trim().toLocaleLowerCase());
}

export function draftSnapshot(data, taxes, sourceVersion) {
  return { data, taxes, sourceVersion };
}
