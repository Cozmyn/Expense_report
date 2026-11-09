import { useState } from 'react';
import { Search, ArrowUpRight, Receipt } from 'lucide-react';
import { money, stateText } from './ReportDisplay';

type Any = Record<string, any>;

export function Stat({ title, value, note, icon }: Any) {
  return (
    <section className="stat panel">
      <div className="stat-top">
        <span>{title}</span>
        <span className="stat-icon">{icon}</span>
      </div>
      <strong>{value}</strong>
      <small>{note}</small>
    </section>
  );
}

export function ReportList({ rows, t, state, open, lang = 'ro' }: Any) {
  const [q, Q] = useState(''),
    [filter, F] = useState('');
  const shown = rows.filter(
    (r: Any) =>
      (r.number + ' ' + r.purpose + ' ' + (r.employeeName || '') + ' ' + (r.managerName || ''))
        .toLowerCase()
        .includes(q.toLowerCase()) &&
      (!filter || r.state === filter),
  );
  const locale = lang === 'en' ? 'en-GB' : 'ro-RO';
  const updated = (r: Any) => new Date(r.updated).toLocaleDateString(locale);
  const age = (r: Any) =>
    r.ageDays ?? Math.floor((Date.now() - new Date(r.created).getTime()) / 86400000);
  const title = (r: Any) => r.purpose || t('Decont fără titlu', 'Untitled report');
  const people = (r: Any) => [r.number, r.employeeName, r.managerName].filter(Boolean).join(' · ');
  const ageLabel = (r: Any) =>
    t(age(r) === 1 ? 'zi' : 'zile', age(r) === 1 ? 'day' : 'days');

  return (
    <>
      <div className="list-toolbar">
        <div className="search">
          <Search size={18} />
          <input
            aria-label={t('Caută deconturi', 'Search reports')}
            placeholder={t('Caută după număr, scop sau persoană…', 'Search by number, purpose or person…')}
            value={q}
            onChange={(e) => Q(e.target.value)}
          />
        </div>
        <select
          aria-label={t('Stare', 'Status')}
          value={filter}
          onChange={(e) => F(e.target.value)}
        >
          <option value="">{t('Toate stările', 'All statuses')}</option>
          {Object.keys(stateText).map((s) => (
            <option key={s} value={s}>
              {state(s)}
            </option>
          ))}
        </select>
      </div>
      <div className="table-wrap report-list-table">
        <table>
          <thead>
            <tr>
              <th>{t('Decont / Scop', 'Report / Purpose')}</th>
              <th>{t('Stare', 'Status')}</th>
              <th>{t('Actualizat', 'Updated')}</th>
              <th className="right">{t('Total', 'Total')}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {shown.map((r: Any) => (
              <tr key={r.id}>
                <td>
                  <button className="report-link" onClick={() => open(r.id)}>
                    {title(r)}
                    <small>
                      {people(r)} · {age(r)} {ageLabel(r)}
                    </small>
                  </button>
                </td>
                <td>
                  <span className={'badge ' + r.state.toLowerCase()}>{state(r.state)}</span>
                </td>
                <td className="muted">{updated(r)}</td>
                <td className="right amount">
                  {money(r.total)} <small>RON</small>
                </td>
                <td>
                  <button
                    className="icon"
                    aria-label={t('Deschide', 'Open')}
                    onClick={() => open(r.id)}
                  >
                    <ArrowUpRight size={17} />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <section className="report-cards" aria-label={t('Deconturi', 'Expense reports')}>
        {shown.map((r: Any) => (
          <article key={r.id} className="report-card">
            <div className="report-card-heading">
              <button className="report-link report-card-title" onClick={() => open(r.id)}>
                {title(r)}
              </button>
              <span className={'badge ' + r.state.toLowerCase()}>{state(r.state)}</span>
            </div>
            <p className="report-card-meta">
              {people(r)} · {age(r)} {ageLabel(r)}
            </p>
            <div className="report-card-facts">
              <div>
                <span>{t('Actualizat', 'Updated')}</span>
                <strong>{updated(r)}</strong>
              </div>
              <div>
                <span>{t('Total', 'Total')}</span>
                <strong>{money(r.total)} RON</strong>
              </div>
            </div>
          </article>
        ))}
      </section>
      {!shown.length && (
        <div className="empty">
          <Receipt size={32} />
          <h3>
            {rows.length
              ? t('Niciun decont găsit.', 'No matching reports.')
              : t('Nu sunt deconturi de afișat.', 'No reports to display.')}
          </h3>
          <p>
            {rows.length
              ? t(
                  'Încearcă altă căutare sau alege „Toate stările”.',
                  'Try a different search or select “All statuses”.',
                )
              : t(
                  'Deconturile disponibile vor apărea aici.',
                  'Available reports will appear here.',
                )}
          </p>
        </div>
      )}
    </>
  );
}
