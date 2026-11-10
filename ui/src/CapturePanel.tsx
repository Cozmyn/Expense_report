import { add } from './decimal.mjs';
import OcrGroupBoard from './OcrGroupBoard';
import React, { useEffect, useRef, useState } from 'react';
import { api, upload as transfer } from './api';
import { newReceipt } from './ReceiptModal';
type Any = Record<string, any>;
export default function CapturePanel({ report, t, run, refresh, reason, edit, editing, masters }: Any) {
  const [working, W] = useState(false),
    [progress, Progress] = useState(0),
    [receiptMode, ReceiptMode] = useState<'Single' | 'Multiple'>('Single'),
    [autoFiles, AutoFiles] = useState<string[]>([]);
  const openedGroups = useRef(new Set<string>());
  const autoOpening = useRef(false);
  const entryVersion = useRef<number | null>(null);
  const stopAutomaticReview = useRef(false);
  useEffect(() => {
    if (editing && entryVersion.current == null) entryVersion.current = report.version;
    if (!editing && entryVersion.current != null) {
      // Closing an unmodified review is an explicit cancellation; do not
      // keep opening more dialogs when the employee needs to regroup pages.
      if (entryVersion.current === report.version) {
        stopAutomaticReview.current = true;
        AutoFiles([]);
      }
      entryVersion.current = null;
    }
  }, [editing, report.version]);
  const files = (report.attachments || []).filter((f: Any) => !f.archived);
  useEffect(() => {
    if (
      files.some(
        (f: Any) =>
          f.scan === 'Pending' ||
          (f.ocr && ['Pending', 'Processing'].includes(JSON.parse(f.ocr).state)),
      )
    ) {
      const timer = setInterval(() => run(refresh), 2500);
      return () => clearInterval(timer);
    }
  }, [report.version, JSON.stringify(files)]);
  const complete = (file: Any) =>
    run(async () => {
      const grant = await api(
        `/reports/${report.id}/document-bundles/${file.id}/authorization`,
        'POST',
        { version: report.version, reason },
      );
      await api(`/reports/${report.id}/document-bundles/${file.id}/complete`, 'POST', {
        version: grant.version,
        token: grant.token,
        reason,
      });
      await refresh();
    });
  const upload = (selected: File[]) =>
    run(async () => {
      W(true);
      let version = report.version;
      try {
        for (const file of selected) {
          Progress(0);
          const hash = Array.from(
            new Uint8Array(await crypto.subtle.digest('SHA-256', await file.arrayBuffer())),
          )
            .map((b) => b.toString(16).padStart(2, '0'))
            .join('');
          const ext = file.name.split('.').pop()?.toLowerCase();
          const mime =
            (
              {
                pdf: 'application/pdf',
                png: 'image/png',
                jpg: 'image/jpeg',
                jpeg: 'image/jpeg',
                heic: 'image/heic',
                heif: 'image/heic',
              } as Any
            )[ext || ''] || file.type;
          const grant = await api('/reports/' + report.id + '/document-bundles', 'POST', {
            version,
            name: file.name,
            size: file.size,
            mime,
            hash,
            reason,
            receiptMode,
          });
          stopAutomaticReview.current = false;
          AutoFiles((old) => [...old, grant.documentId]);
          const form = new FormData();
          form.append('version', String(version));
          form.append('reason', reason);
          form.append('file', file);
          const result = await transfer(grant.uploadUrl, form, Progress, {
            'X-Upload-Token': grant.token,
          });
          version = result.version;
          const done = await api(
            `/reports/${report.id}/document-bundles/${grant.documentId}/complete`,
            'POST',
            { version, token: grant.token, reason },
          );
          version = done.version;
        }
      } finally {
        W(false);
        await refresh();
      }
    });
  const propose = (file: Any, candidate: Any) =>
    run(async () => {
      const latest = await api(`/reports/${report.id}/documents/${file.id}/review`);
      const group = latest.review?.candidates?.find((g: Any) => g.id === candidate.sourceGroupId);
      if (!group || group.state === 'Verified')
        throw new Error(
          t(
            'Grupul a fost modificat. Actualizează decontul și deschide bonul existent.',
            'The group changed. Refresh the report and open the existing receipt.',
          ),
        );
      candidate = { ...group, sourceGroupId: group.id };
      if (latest.review.version !== report.version) await refresh();
      const receipt = newReceipt();
      const data: Any = {
        ...receipt.data,
        date: '',
        country: '',
        currency: '',
        taxCurrency: '',
        amount: '',
      };
      for (const [key, f] of Object.entries(candidate.fields) as [string, Any][]) {
        if (
          [
            'supplier',
            'cui',
            'invoice',
            'date',
            'currency',
            'country',
            'documentType',
            'description',
          ].includes(key) &&
          f.value != null
        )
          data[key] = f.value;
        if (key === 'amount' && f.value != null) data.amount = String(f.value);
        if (key === 'hasCompanyCui') data.hasCompanyCui = f.value === 'true';
      }
      data.taxCurrency = data.currency;
      const totals: Any = {};
      for (const tax of candidate.taxes || []) {
        totals[tax.rate] ??= { base: 0, vat: 0 };
        totals[tax.rate].base = add(totals[tax.rate].base, tax.base);
        totals[tax.rate].vat = add(totals[tax.rate].vat, tax.vat);
      }
      // Human-selected VAT source regions override OCR suggestions; never
      // infer unknown paid tax from a zero default.
      for (const [key, field] of Object.entries(candidate.fields || {}) as [string, Any][]) {
        const match = /^tax(Base|Vat):(\d{1,2}(?:\.\d+)?)$/.exec(key);
        if (!match || field?.value == null) continue;
        const rate = match[2];
        totals[rate] ??= { base: '', vat: '' };
        totals[rate][match[1] === 'Base' ? 'base' : 'vat'] = String(field.value);
      }
      data.taxes = Object.entries(totals).flatMap(([rate, value]: [string, any]) => {
        const master = masters.find(
          (m: Any) =>
            m.kind === 'vat' &&
            m.active &&
            m.from <= data.date &&
            (!m.to || m.to >= data.date) &&
            Number(JSON.parse(m.json).rate) === Number(rate),
        );
        return master ? [{ rateId: master.id, ...value }] : [];
      });
      if (candidate.defaults) {
        data.costCenter = candidate.defaults.costCenter || data.costCenter;
        data.project = candidate.defaults.project || data.project;
      }
      Object.assign(data, candidate.correctedExpense || {});
      edit({
        ...receipt,
        data,
        ocrVersion: latest.review.version,
        sourceGroupId: candidate.sourceGroupId,
        sourceAttachmentId: file.id,
        sourceOriginal: {
          ...file,
          url:
            '/api/report-attachments/' +
            file.id +
            (file.mime === 'application/pdf' ? '#page=' + (candidate.pages?.[0] || 1) : ''),
          previewUrl:
            file.mime === 'image/heic'
              ? `/api/reports/${report.id}/documents/${file.id}/preview`
              : null,
        },
        ocrCandidate: candidate,
      });
    });

  // Open each OCR draft in sequence; unverified numbers are not saved to accounting.
  useEffect(() => {
    if (!autoFiles.length || editing || autoOpening.current || stopAutomaticReview.current) return;
    const id = autoFiles[0];
    const file = files.find((f: Any) => f.id === id);
    if (!file) return;
    const ocr = file.ocr ? JSON.parse(file.ocr) : null;
    if (file.scan === 'Rejected' || ocr?.state === 'Failed') {
      AutoFiles((old) => old.slice(1));
      return;
    }
    if (file.scan !== 'Clean' || !ocr ||
        !['ReviewRequired', 'PartiallyProcessed', 'ManualEntryRequired'].includes(ocr.state)) return;
    let active = true;
    autoOpening.current = true;
    api('/reports/' + report.id + '/documents/' + id + '/review')
      .then((result: Any) => {
        if (!active) return;
        const group = result.review?.candidates?.find(
          (candidate: Any) =>
            candidate.state !== 'Verified' && !openedGroups.current.has(candidate.id),
        );
        if (group) {
          openedGroups.current.add(group.id);
          propose(file, { ...group, sourceGroupId: group.id });
        } else {
          AutoFiles((old) => old[0] === id ? old.slice(1) : old);
        }
      })
      .catch(() => {
        if (active) AutoFiles((old) => old[0] === id ? old.slice(1) : old);
      })
      .finally(() => { autoOpening.current = false; });
    return () => { active = false; };
  }, [autoFiles.join(','), editing, report.version, JSON.stringify(files)]);
  return (
    <section className="panel capture-panel">
      <h2>{t('Încarcă documentele și extrage datele', 'Upload documents and extract details')}</h2>
      <p>
        {t(
          'Încarcă o fotografie sau un PDF. Verifici fiecare bon înainte de salvare.',
          'Upload a photo or PDF. You review each receipt before saving.',
        )}
      </p>
      <p className="small muted">
        {t('Alege câte bonuri conține fișierul. După citire se deschide automat primul bon, cu documentul alături.',
           'Choose how many receipts the file contains. After reading, the first receipt opens automatically beside its source.')}
      </p>
      <fieldset className="receipt-mode-picker">
        <legend>{t('Ce conține documentul?', 'What does the document contain?')}</legend>
        <label className={receiptMode === 'Single' ? 'receipt-mode active' : 'receipt-mode'}>
          <input type="radio" name="receipt-mode" value="Single"
            checked={receiptMode === 'Single'} disabled={working}
            onChange={() => ReceiptMode('Single')} />
          <strong>{t('Un singur bon / o factură', 'One receipt / one invoice')}</strong>
          <span>{t('Chiar dacă documentul are mai multe pagini.', 'Even if it has multiple pages.')}</span>
        </label>
        <label className={receiptMode === 'Multiple' ? 'receipt-mode active' : 'receipt-mode'}>
          <input type="radio" name="receipt-mode" value="Multiple"
            checked={receiptMode === 'Multiple'} disabled={working}
            onChange={() => ReceiptMode('Multiple')} />
          <strong>{t('Mai multe bonuri', 'Multiple receipts')}</strong>
          <span>{t('Le verifici unul câte unul, cu bonul curent evidențiat.', 'Review each receipt separately with its source highlighted.')}</span>
        </label>
      </fieldset>
      <label className="field receipt-upload">
        <span>{t('Adaugă PDF sau fotografie', 'Add a PDF or photo')}</span>
        <input type="file" multiple={receiptMode === 'Multiple'}
          disabled={working} accept=".pdf,.jpg,.jpeg,.png,.heic,.heif"
          onChange={(e) => {
            upload(Array.from(e.target.files || []));
            e.target.value = '';
          }} />
      </label>
      <p className="small muted">
        {t('Acesta este singurul loc unde încarci documentele. Poți fotografia bonul direct din selectorul telefonului.',
           'This is the only upload location. You can take a photo from the phone file picker.')}
      </p>
      {working && (
        <p role="status">
          {t('Se încarcă', 'Uploading')} {progress}% <progress max={100} value={progress} />
        </p>
      )}
      {files.map((file: Any) => {
        const ocr = file.ocr ? JSON.parse(file.ocr) : null;
        return (
          <div className="capture-document" key={file.id}>
            <h3>{file.name}</h3>
            <p>
              {file.scan === 'Clean'
                ? t('Scanare completă', 'Security scan complete')
                : file.scan === 'Rejected'
                  ? t('Document respins', 'Document rejected')
                  : t('Scanare în curs…', 'Scanning…')}
            </p>
            {ocr?.state === 'Processing' && (
              <p role="status">
                {t(
                  (
                    {
                      Normalizing: 'Se normalizează documentul',
                      Rendering: 'Se pregătesc paginile',
                      Extracting: 'Se citesc paginile',
                      Validating: 'Se verifică propunerile',
                    } as Any
                  )[ocr.stage] || 'Procesare',
                  (
                    {
                      Normalizing: 'Normalizing document',
                      Rendering: 'Preparing pages',
                      Extracting: 'Reading pages',
                      Validating: 'Checking proposals',
                    } as Any
                  )[ocr.stage] || 'Processing',
                )}{' '}
                · {ocr.completedPages}/{ocr.totalPages || '…'} {t('pagini', 'pages')}
                {ocr.failedPages > 0
                  ? ' · ' +
                    ocr.failedPages +
                    ' ' +
                    t('necesită verificare manuală', 'need manual review')
                  : ''}
              </p>
            )}
            {file.scan === 'AwaitingCompletion' && (
              <div className="issue warning">
                <p>
                  {t(
                    'Fișierul este păstrat, dar încărcarea trebuie finalizată înainte de scanare.',
                    'File is retained, but upload must be completed before scanning.',
                  )}
                </p>
                <button className="secondary" onClick={() => complete(file)}>
                  {t('Finalizează încărcarea', 'Complete upload')}
                </button>
              </div>
            )}
            {file.scan === 'Clean' && (
              <div className="actions">
                <a
                  className="secondary"
                  href={'/api/report-attachments/' + file.id}
                  target="_blank"
                  rel="noreferrer"
                >
                  {t('Vezi originalul', 'View original')}
                </a>
                <button
                  className="secondary"
                  disabled={['Pending', 'Processing'].includes(ocr?.state)}
                  onClick={() =>
                    run(async () => {
                      await api(`/reports/${report.id}/documents/${file.id}/extract`, 'POST', {
                        version: report.version,
                        reason,
                      });
                      await refresh();
                    })
                  }
                >
                  {['Pending', 'Processing'].includes(ocr?.state)
                    ? t('Extragere în curs…', 'Extracting…')
                    : t('Extrage / reîncearcă OCR', 'Extract / retry OCR')}
                </button>
              </div>
            )}
            {ocr?.state === 'Failed' && (
              <div className="issue warning">
                <p>
                  {t(
                    'Extragerea a eșuat. Poți completa manual bonul și păstra acest original.',
                    'Extraction failed. You can enter the receipt manually and retain this original.',
                  )}
                </p>
                <button
                  className="secondary"
                  onClick={() =>
                    edit({
                      ...newReceipt(),
                      sourceAttachmentId: file.id,
                      sourceOriginal: { ...file, url: '/api/report-attachments/' + file.id },
                    })
                  }
                >
                  {t('Completează manual din acest original', 'Enter manually from this original')}
                </button>
              </div>
            )}
            {file.scan === 'Clean' && (
              <OcrGroupBoard
                file={file}
                report={report}
                t={t}
                run={run}
                refresh={refresh}
                propose={(candidate: Any) => propose(file, candidate)}
                linkExisting={(candidate: Any, line: Any) =>
                  edit({
                    ...line,
                    sourceGroupId: candidate.sourceGroupId,
                    sourceAttachmentId: file.id,
                    ocrCandidate: candidate,
                    sourceOriginal: {
                      ...file,
                      url:
                        '/api/report-attachments/' +
                        file.id +
                        (file.mime === 'application/pdf'
                          ? '#page=' + (candidate.pages?.[0] || 1)
                          : ''),
                      previewUrl:
                        file.mime === 'image/heic'
                          ? '/api/reports/' + report.id + '/documents/' + file.id + '/preview'
                          : null,
                    },
                  })
                }
              />
            )}
            {ocr?.text && (
              <section className="ocr-transcript">
                <h4>{t('Textul citit din document', 'Text read from the document')}</h4>
                <p className="small muted">{t('Textul brut nu înseamnă automat câmpuri recunoscute. Verifică propunerile de mai sus.',
                  'Raw text is not the same as recognised form values. Review the proposals above.')}</p>
                <pre className="ocr-text">{ocr.text}</pre>
              </section>
            )}
          </div>
        );
      })}
    </section>
  );
}
