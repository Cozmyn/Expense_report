import { add } from './decimal.mjs';
import OcrGroupBoard from './OcrGroupBoard';
import React, { useEffect, useState } from 'react';
import { api, upload as transfer } from './api';
import { newReceipt } from './ReceiptModal';
type Any = Record<string, any>;
export default function CapturePanel({ report, t, run, refresh, reason, edit, editing, masters }: Any) {
  const [working, W] = useState(false),
    [progress, Progress] = useState(0),
    [receiptMode, ReceiptMode] = useState<'Single' | 'Multiple'>('Single'),
    [queued, Queued] = useState<Array<{ file: File; mode: 'Single' | 'Multiple' }>>([]),
    [activeFile, ActiveFile] = useState(''),
    [completedFiles, CompletedFiles] = useState(0);
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
  const upload = (selected: Array<{ file: File; mode: 'Single' | 'Multiple' }>) =>
    run(async () => {
      W(true);
      let version = report.version;
      CompletedFiles(0);
      let succeeded = 0;
      try {
        for (const [index, item] of selected.entries()) {
          const { file, mode } = item;
          ActiveFile(file.name);
          CompletedFiles(index);
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
            receiptMode: mode,
          });
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
          succeeded = index + 1;
          CompletedFiles(succeeded);
        }
        Queued([]);
      } catch (error) {
        // Keep only unfinished files selected to avoid creating duplicate documents on retry.
        Queued(selected.slice(succeeded));
        throw error;
      } finally {
        W(false);
        ActiveFile('');
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

  // UX-02: do not interrupt the user with automatically opened receipt
  // dialogs. The stable page/group workspace remains available after OCR.
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
        {t('Alege câte bonuri conține fișierul. După citire, selectezi tu bonul din lista stabilă, cu originalul și formularul alături.',
           'Choose how many receipts the file contains. After reading, choose the receipt from the stable list, with source and form side by side.')}
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
        <input type="file" multiple
          disabled={working} accept=".pdf,.jpg,.jpeg,.png,.heic,.heif"
          onChange={(e) => {
            const selected = Array.from(e.target.files || []);
            if (selected.length === 1) {
              Queued([]);
              void upload([{ file: selected[0], mode: receiptMode }]);
            } else if (selected.length > 1) {
              Queued(selected.map((file) => ({ file, mode: receiptMode })));
            }
            e.target.value = '';
          }} />
      </label>
      <p className="small muted">
        {t('Poți încărca mai multe fișiere, chiar dacă fiecare conține câte un singur bon. Pentru selecții multiple verifici tipul fiecărui fișier înainte de trimitere. Pe telefon poți fotografia bonul din selector.',
           'You can upload multiple files even when each contains one receipt. For multi-file selections, review each file type before uploading. On mobile you can take a photo from the file picker.')}
      </p>
      {queued.length > 0 && !working && (
        <section className="capture-file-queue" aria-label={t('Fișiere pregătite pentru încărcare', 'Files ready to upload')}>
          <h3>{t('Verifică fișierele selectate', 'Review selected files')} ({queued.length})</h3>
          <p className="small muted">
            {t('Alege ce conține fiecare fișier, nu câte fișiere încarci.', 'Choose what each file contains, independently of the file count.')}
          </p>
          {queued.map((item, index) => (
            <div className="capture-queued-file" key={index}>
              <span className="capture-file-name">{item.file.name}</span>
              <label>
                <span className="small">{t('Conținut fișier', 'File content')}</span>
                <select value={item.mode} onChange={(event) =>
                  Queued((old) => old.map((entry, i) => i === index
                    ? { ...entry, mode: event.target.value as 'Single' | 'Multiple' } : entry))}>
                  <option value="Single">{t('Un bon sau o factură', 'One receipt or invoice')}</option>
                  <option value="Multiple">{t('Mai multe bonuri', 'Multiple receipts')}</option>
                </select>
              </label>
              <button className="text-button" type="button" onClick={() =>
                Queued((old) => old.filter((_, i) => i !== index))}>
                {t('Elimină din selecție', 'Remove from selection')}
              </button>
            </div>
          ))}
          <div className="actions">
            <button type="button" className="primary" onClick={() => void upload(queued)}>
              {t('Încarcă fișierele selectate', 'Upload selected files')} ({queued.length})
            </button>
            <button type="button" className="secondary" onClick={() => Queued([])}>
              {t('Anulează selecția', 'Cancel selection')}
            </button>
          </div>
        </section>
      )}
      {working && (
        <div role="status" className="capture-upload-status">
          <p>{t('Fișierul', 'File')} {completedFiles + 1}: <strong>{activeFile}</strong></p>
          <label>{t('Progres fișier curent', 'Current file progress')} {progress}%
            <progress max={100} value={progress} />
          </label>
        </div>
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
