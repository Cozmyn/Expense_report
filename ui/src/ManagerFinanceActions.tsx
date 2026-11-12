import { ArrowUpRight, CheckCheck } from 'lucide-react';
import { useState } from 'react';

type Any = Record<string, any>;
type Translate = (ro: string, en: string) => string;

/** The actionable set comes from the server's canApprove flag; we never infer permissions from state. */
export function ManagerNextActions({
  reports, open, t,
}: { reports: Any[]; open: (id: string) => void; t: Translate }) {
  const [showAll, ShowAll] = useState(false);
  const pending = reports
    .filter((report) => report.canApprove)
    .sort((a, b) => (b.ageDays || 0) - (a.ageDays || 0));
  return (
    <section className="panel manager-next-actions">
      <div className="section-heading">
        <div>
          <h2>{t('Deconturi care așteaptă decizia ta', 'Reports awaiting your decision')}</h2>
          <p className="muted">
            {pending.length
              ? t('Deschide un decont pentru documente și detalii înainte de decizie.',
                  'Open a report to review receipts and details before deciding.')
              : t('Nu ai aprobări în așteptare.', 'You have no pending approvals.')}
          </p>
        </div>
        <strong aria-label={t('Aprobări în așteptare', 'Pending approvals')}>
          {pending.length}
        </strong>
      </div>
      {(showAll ? pending : pending.slice(0, 4)).map((report) => (
        <button key={report.id} className="simple-row row-button" onClick={() => open(report.id)}>
          <span>
            <strong>{report.purpose || report.number}</strong>
            <small>
              {report.number} · {report.employeeName || t('Angajat', 'Employee')} ·{' '}
              {report.ageDays || 0} {t('zile', 'days')}
              {' · '}{report.lines?.length || 0} {t('bonuri', 'receipts')}
              {' · '}{Number(report.total || 0).toLocaleString(undefined, { maximumFractionDigits: 2 })} RON
            </small>
          </span>
          <span className="manager-action-open">
            <CheckCheck size={18} aria-hidden="true" />
            {t('Verifică', 'Review')}
            <ArrowUpRight size={15} aria-hidden="true" />
          </span>
        </button>
      ))}
      {pending.length > 4 && (
        <button
          type="button"
          className="secondary manager-show-more"
          aria-expanded={showAll}
          onClick={() => ShowAll((value) => !value)}
        >
          {showAll
            ? t('Afișează numai primele 4 aprobări', 'Show only the first 4 approvals')
            : t('Arată toate aprobările', 'Show every pending approval') + ' (' + pending.length + ')'}
        </button>
      )}
    </section>
  );
}
