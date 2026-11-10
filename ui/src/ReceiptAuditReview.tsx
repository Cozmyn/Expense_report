import React, { useState } from 'react';
import DocumentViewer from './DocumentViewer';
import { ocrFieldLabel } from './OcrLabels';

type Any = Record<string, any>;
type Region = { page: number; x: number; y: number; width: number; height: number };

const readJson = (value: string | null | undefined, fallback: any) => {
  try { return value ? JSON.parse(value) : fallback; } catch { return fallback; }
};
const normalize = (value: any) => String(value ?? '').replace(/\s+/g, ' ').trim().toLowerCase();
const fieldsInOrder = ['supplier', 'cui', 'invoice', 'date', 'country', 'currency', 'amount', 'description'];
const regionOf = (field: Any): Region | null =>
  field?.box && field?.page ? { page: field.page, ...field.box } : null;

/**
 * Review-only document/evidence workspace for approvers and Finance.
 * Financial edits continue exclusively through the authorised receipt editor.
 */
export default function ReceiptAuditReview({
  report, detail, masters, t, editable, finance, onEdit,
}: {
  report: Any; detail: Any; masters: Any[]; t: (ro: string, en: string) => string;
  editable: boolean; finance: boolean; onEdit: (line: Any) => void;
}) {
  const [selectedId, Selected] = useState<string | null>(null);
  const [focused, Focus] = useState<Region | null>(null);
  const [focusToken, Token] = useState(0);
  const lines: Any[] = report.lines || [];
  if (!lines.length) return null;
  const current = lines.find((l) => l.id === selectedId) || lines[0];
  const group = detail.sourceGroups?.find((g: Any) => g.expenseId === current.id);
  const attachment = report.attachments?.find((a: Any) => a.id === group?.bundleId)
    || current.evidence?.find((e: Any) => e.scan === 'Clean');
  const ocrFields = readJson(group?.fields, {});
  const ocrTaxes: Any[] = readJson(group?.taxes, []);
  const sourceRegions: Region[] = readJson(group?.regions, []);
  const savedTaxRows = (current.data?.taxes || []).map((tax: Any) => {
    const master = masters.find((m) => m.id === tax.rateId);
    const rate = Number(readJson(master?.json, {})?.rate ?? 0);
    return { rate, base: Number(tax.base), vat: Number(tax.vat) };
  });
  const savedTax = (rate: number) => savedTaxRows.find((row: Any) => row.rate === rate);
  const evidenceFields = Object.entries(ocrFields) as [string, Any][];
  const ordered = [
    ...fieldsInOrder.filter((key) => ocrFields[key]),
    ...evidenceFields.map(([key]) => key).filter((key) => !fieldsInOrder.includes(key)),
  ];
  const isMatch = (key: string) => {
    const ocr = normalize(ocrFields[key]?.value);
    const receipt = normalize(current.data?.[key]);
    if (key === 'amount') return Number(ocr) === Number(receipt);
    return ocr !== '' && ocr === receipt;
  };
  const matched: Region[] = [
    ...ordered.filter(isMatch)
      .map((key) => regionOf(ocrFields[key]))
      .filter((region): region is Region => region != null),
    ...ocrTaxes.filter((tax: Any) => {
      const saved = savedTax(Number(tax.rate));
      return saved && saved.base === Number(tax.base) && saved.vat === Number(tax.vat)
        && tax.box && tax.page;
    }).map((tax: Any) => ({ page: tax.page, ...tax.box })),
  ];
  const mismatches = ordered.filter((key) => !isMatch(key));
  const document = attachment ? {
    ...attachment,
    reportId: report.id,
    url: '/api/report-attachments/' + attachment.id,
    previewUrl: attachment.mime === 'image/heic'
      ? '/api/reports/' + report.id + '/documents/' + attachment.id + '/preview'
      : null,
  } : null;
  const choose = (line: Any) => {
    Selected(line.id);
    Focus(null);
    Token((n) => n + 1);
  };

  return (
    <section className="panel receipt-audit-review"
      aria-label={t('Verificare bonuri și OCR', 'Receipt and OCR review')}>
      <div className="section-heading">
        <div>
          <h2>{t('Verifică bonul în original', 'Review receipt against original')}</h2>
          <p className="small muted">{t(
            'Compară datele OCR cu cheltuiala salvată. Apasă pe un câmp pentru a vedea textul din care a fost citit.',
            'Compare OCR suggestions with the saved expense. Select a field to locate its source text.'
          )}</p>
        </div>
        <span className="small muted">
          {lines.length} {t('bonuri', 'receipts')}
        </span>
      </div>
      <div className="receipt-review-selector" role="group"
        aria-label={t('Bonuri de verificat', 'Receipts to review')}>
        {lines.map((line) => (
          <button key={line.id} type="button"
            className={line.id === current.id ? 'primary' : 'secondary'}
            aria-pressed={line.id === current.id}
            onClick={() => choose(line)}>
            {t('Bon', 'Receipt')} {line.position} · {line.data?.supplier || line.data?.category || '—'}
          </button>
        ))}
      </div>
      <div className="receipt-review-grid">
        <div className="receipt-review-source">
          {document ? (
            <DocumentViewer
              key={document.id}
              document={document}
              t={t}
              regions={sourceRegions}
              highlights={matched}
              focus={focused || sourceRegions[0] || null}
              focusPage={sourceRegions[0]?.page || 1}
              focusToken={focusToken}
            />
          ) : (
            <div className="issue warning" role="status">
              {t('Nu există un original OCR asociat acestui bon. Verifică documentele justificative.',
                 'No OCR source is linked to this receipt. Check its supporting documents.')}
            </div>
          )}
        </div>
        <div className="receipt-review-fields">
          <h3>{t('Bon', 'Receipt')} {current.position}: {current.data?.supplier || current.data?.description}</h3>
          <p>
            <strong>{current.data?.amount} {current.data?.currency}</strong>
            {' · '}
            {t('Centru de cost', 'Cost centre')}: <strong>{current.data?.costCenter || '—'}</strong>
          </p>
          {group && <p className="small muted">
            {t('Stare OCR', 'OCR state')}: <strong>{group.state || 'Verified'}</strong>
            {group.verifiedAt && ' · ' + new Date(group.verifiedAt).toLocaleString()}
          </p>}
          <div className="receipt-review-summary" role="status">
            <strong>{ordered.filter(isMatch).length}/{ordered.length}</strong>
            {' '}{t('câmpuri OCR identice cu cele salvate', 'OCR fields matching saved values')}
            {mismatches.length > 0 && <p className="small muted">
              {t('Diferențele pot fi corecții umane legitime; verifică originalul, nu înlocui automat valoarea.',
                 'Differences can be legitimate human corrections; verify the original instead of overwriting the value automatically.')}
            </p>}
          </div>
          {ordered.length > 0 ? (
            <div className="receipt-review-field-list">
              {ordered.map((key) => {
                const field = ocrFields[key];
                const region = regionOf(field);
                const match = isMatch(key);
                return (
                  <button type="button" key={key} className="receipt-review-field"
                    disabled={!region}
                    onClick={() => { Focus(region); Token((n) => n + 1); }}>
                    <strong>{ocrFieldLabel(key, t)}</strong>
                    <span>{t('OCR', 'OCR')}: {String(field.value ?? '—')}</span>
                    <span>{t('Salvat', 'Saved')}: {String(current.data?.[key] ?? '—')}</span>
                    <span className={match ? 'match' : 'needs-review'}>
                      {match ? t('Coincide', 'Matches') : t('Verifică diferența', 'Review difference')}
                      {field.page && ' · ' + t('pagina', 'page') + ' ' + field.page}
                    </span>
                  </button>
                );
              })}
            </div>
          ) : (
            <div className="issue warning">
              {t('OCR nu a identificat câmpuri structurate; datele trebuie comparate manual cu originalul.',
                 'OCR identified no structured fields. Compare saved data manually with the original.')}
            </div>
          )}
          {(savedTaxRows.length > 0 || ocrTaxes.length > 0) && (
            <section className="receipt-review-tax">
              <h4>{t('Verificarea bazei și TVA', 'Tax base and VAT reconciliation')}</h4>
              <p className="small muted">{t(
                'Compară baza și TVA-ul efectiv cu propunerile OCR. Suma bonului trebuie să corespundă valorilor de pe document.',
                'Compare taxable base and actual VAT with OCR proposals. The gross amount must match the original document.'
              )}</p>
              <div className="table-wrap">
                <table>
                  <thead><tr>
                    <th>{t('Cotă', 'Rate')}</th>
                    <th>{t('Bază salvată', 'Saved base')}</th>
                    <th>{t('TVA salvat', 'Saved VAT')}</th>
                    <th>{t('Propunere OCR', 'OCR proposal')}</th>
                  </tr></thead>
                  <tbody>
                    {Array.from(new Set([
                      ...savedTaxRows.map((v: Any) => v.rate),
                      ...ocrTaxes.map((v: Any) => Number(v.rate)),
                    ])).sort((a, b) => a - b).map((rate) => {
                      const saved = savedTax(rate);
                      const original = ocrTaxes.find((v: Any) => Number(v.rate) === rate);
                      return <tr key={rate}>
                        <td>{rate}%</td>
                        <td>{saved?.base ?? '—'}</td>
                        <td>{saved?.vat ?? '—'}</td>
                        <td>{original
                          ? String(original.base) + ' + ' + String(original.vat)
                          : t('Nu a fost citit', 'Not recognised')}</td>
                      </tr>;
                    })}
                  </tbody>
                </table>
              </div>
            </section>
          )}
          {editable && (
            <button type="button" className="primary"
              onClick={() => onEdit(current)}>
              {finance
                ? t('Corectează bonul și câmpurile OCR', 'Correct receipt and OCR fields')
                : t('Corectează bonul', 'Correct receipt')}
            </button>
          )}
          {!editable && <p className="small muted">{t(
            'Mod de verificare fără editare. Observațiile pot fi adăugate în secțiunea de comentarii.',
            'Read-only review. Add observations in the report comments section.'
          )}</p>}
        </div>
      </div>
    </section>
  );
}
