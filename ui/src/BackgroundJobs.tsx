import { useEffect, useState } from 'react';
import { api } from './api';
import { Field } from './Fields';

type Any = Record<string, any>;

export function JobsPage({ t, run }: Any) {
  const [metrics, Metrics] = useState<Any | null>(null);
  const [rows, R] = useState<Any[]>([]),
    [reason, Reason] = useState(''),
    [health, H] = useState<Any | null>(null);
  const load = async () => {
    R(await api('/admin/jobs'));
    H(await api('/admin/health'));
    Metrics(await api('/admin/ocr-metrics'));
  };
  useEffect(() => {
    run(load);
  }, []);
  return (
    <section className="panel">
      <h1>{t('Operațiuni de fundal', 'Background jobs')}</h1>
      {metrics && (
        <section>
          <h2>{t('Procesarea documentelor', 'Document processing')}</h2>
          <div className="form-grid">
            {Object.entries(metrics).map(([key, value]) => (
              <p key={key}>
                <strong>
                  {t(
                    (
                      {
                        documents: 'Documente',
                        sourcePages: 'Pagini sursă',
                        verifiedGroups: 'Grupuri verificate',
                        needsReview: 'Grupuri de verificat',
                        quarantined: 'În carantină',
                        queuedJobs: 'Operațiuni în coadă',
                        failedJobs: 'Operațiuni eșuate',
                        outboxPending: 'Evenimente de livrat',
                        verifiedEvents: 'Verificări înregistrate',
                        correctionEvents: 'Intervenții înregistrate',
                      } as Any
                    )[key] || key,
                    (
                      {
                        documents: 'Documents',
                        sourcePages: 'Source pages',
                        verifiedGroups: 'Verified groups',
                        needsReview: 'Groups to review',
                        quarantined: 'Quarantined',
                        queuedJobs: 'Queued jobs',
                        failedJobs: 'Failed jobs',
                        outboxPending: 'Pending events',
                        verifiedEvents: 'Recorded verifications',
                        correctionEvents: 'Recorded corrections',
                      } as Any
                    )[key] || key,
                  )}
                </strong>
                : {String(value)}
              </p>
            ))}
          </div>
          <p className="small muted">
            {t(
              'Intervențiile nu reprezintă o măsură a preciziei OCR. Precizia se evaluează separat, pe un set etichetat.',
              'Intervention counts are not an OCR accuracy measure. Accuracy requires a separately labelled corpus.',
            )}
          </p>
        </section>
      )}
      {health && (
        <div className="form-grid">
          {Object.entries(health).map(([key, value]) => (
            <p key={key}>
              <strong>
                {t(
                  (
                    {
                      ocrStatus: 'Stare OCR',
                      ocrExtractorVersion: 'Versiune parser',
                      ocrTextEngine: 'Motor text PDF',
                      ocrImageEngine: 'Motor imagine',
                      ocrMissingLanguages: 'Limbi indisponibile',
                      ocrNormalization: 'Normalizare PDF',
                      ocrImageRotation: 'Corecție orientare',
                      database: 'Bază de date',
                      scanner: 'Scanare antivirus',
                      smtp: 'Livrare e-mail',
                      storage: 'Stocare',
                      ocrProvider: 'Furnizor OCR',
                      ocrLanguages: 'Limbi OCR',
                      queuedJobs: 'Operațiuni în coadă',
                      failedJobs: 'Operațiuni eșuate',
                      schemaVersion: 'Versiune schemă',
                    } as Any
                  )[key] || key,
                  (
                    {
                      ocrStatus: 'OCR status',
                      ocrExtractorVersion: 'Parser version',
                      ocrTextEngine: 'PDF text engine',
                      ocrImageEngine: 'Image engine',
                      ocrMissingLanguages: 'Unavailable languages',
                      ocrNormalization: 'PDF normalization',
                      ocrImageRotation: 'Orientation recovery',
                      database: 'Database',
                      scanner: 'Virus scanning',
                      smtp: 'Email delivery',
                      storage: 'Storage',
                      ocrProvider: 'OCR provider',
                      ocrLanguages: 'OCR languages',
                      queuedJobs: 'Queued jobs',
                      failedJobs: 'Failed jobs',
                      schemaVersion: 'Schema version',
                    } as Any
                  )[key] || key,
                )}
              </strong>
              : {String(value)}
            </p>
          ))}
        </div>
      )}
      <button className="secondary" onClick={() => run(load)}>
        {t('Actualizează starea', 'Refresh status')}
      </button>
      <Field name={t('Motiv pentru reîncercare', 'Retry reason')}>
        <input value={reason} onChange={(e) => Reason(e.target.value)} />
      </Field>
      {rows.map((j) => (
        <div className="simple-row" key={j.id}>
          <span>
            <strong>
              {j.kind} · {j.state}
            </strong>
            <small>
              {j.attempts} {t('încercări', 'attempts')} · {new Date(j.due).toLocaleString()}
            </small>
            <small>{j.error}</small>
          </span>
          {j.state === 'Failed' && (
            <button
              className="secondary"
              disabled={reason.length < 3}
              onClick={() =>
                run(async () => {
                  await api('/admin/jobs/' + j.id + '/retry', 'POST', { reason });
                  await load();
                })
              }
            >
              {t('Reîncearcă', 'Retry')}
            </button>
          )}
        </div>
      ))}
    </section>
  );
}
export function ReportJobs({ report, t, run }: Any) {
  const [jobs, J] = useState<Any[]>([]);
  const load = () => api('/reports/' + report.id + '/jobs').then(J);
  useEffect(() => {
    run(load);
  }, [report.version]);
  return (
    <section className="panel">
      <div className="section-heading">
        <h2>{t('Documente și livrare notificări', 'Documents & notification delivery')}</h2>
        <button className="secondary" onClick={() => run(load)}>
          {t('Actualizează', 'Refresh')}
        </button>
      </div>
      {jobs.map((j) => (
        <div className="simple-row" key={j.id}>
          <strong>{j.kind}</strong>
          <span>
            {t(
              (
                {
                  Pending: 'În așteptare',
                  Processing: 'În procesare',
                  Completed: 'Finalizat',
                  Failed: 'Eșuat',
                } as Record<string, string>
              )[j.state as string] || j.state,
              j.state,
            )}{' '}
            · {j.attempts} {t('încercări', 'attempts')}
          </span>
        </div>
      ))}
      {!jobs.length && <p>{t('Nicio operațiune înregistrată.', 'No recorded operations.')}</p>}
    </section>
  );
}
