type Translate = (ro: string, en: string) => string;

const names: Record<string, [string, string]> = {
  supplier: ['Furnizor', 'Supplier'],
  cui: ['CUI furnizor', 'Supplier VAT ID'],
  invoice: ['Număr document', 'Document number'],
  date: ['Data documentului', 'Document date'],
  currency: ['Monedă', 'Currency'],
  amount: ['Total', 'Total'],
  country: ['Țara furnizorului', 'Supplier country'],
  hasCompanyCui: ['CUI-ul companiei pe document', 'Company VAT ID on document'],
  description: ['Descriere', 'Description'],
  documentType: ['Tip document', 'Document type'],
};

export function ocrFieldLabel(key: string, t: Translate): string {
  const base = /^taxBase:(\d+(?:\.\d+)?)$/.exec(key);
  if (base) return t('Bază TVA ' + base[1] + '%', 'VAT base ' + base[1] + '%');
  const paid = /^taxVat:(\d+(?:\.\d+)?)$/.exec(key);
  if (paid) return t('TVA plătit ' + paid[1] + '%', 'Paid VAT ' + paid[1] + '%');
  const name = names[key];
  return name ? t(name[0], name[1]) : key;
}
