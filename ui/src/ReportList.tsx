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
    [filter, F] = useState(''),
    [order, Order] = useState('default');
  const shown = rows.filter(
    (r: Any) =>
      (r.number + ' ' + r.purpose + ' ' + (r.employeeName || '') + ' ' + (r.managerName || ''))
        .toLowerCase()
        .includes(q.toLowerCase()) &&
      (!filter || r.state === filter),
  ).sort((a: Any, b: Any) => {
    if (order === 'newest') return new Date(b.updated).getTime() - new Date(a.updated).getTime();
    if (order === 'oldest') return new Date(a.updated).getTime() - new Date(b.updated).getTime();
    if (order === 'amount-high') return Number(b.total || 0) - Number(a.total || 0);
    if (order === 'amount-low') return Number(a.total || 0) - Number(b.total || 0);
    return 0; // preserve ordering supplied by the caller
  });
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
        <select aria-label={t('Ordonează deconturile', 'Sort reports')}
          value={order} onChange={(event) => Order(event.target.value)}>
          <option value="default">{t('Ordine implicită', 'Default order')}</option>
          <option value="newest">{t('Actualizate recent', 'Recently updated')}</option>
          <option value="oldest">{t('Actualizate demult', 'Least recently updated')}</option>
          <option value="amount-high">{t('Sumă descrescător', 'Amount, high to low')}</option>
          <option value="amount-low">{t('Sumă crescător', 'Amount, low to high')}</option>
        </select>
        {(q || filter || order !== 'default') && (
          <button type="button" className="secondary" onClick={() => {
            Q('');
            F('');
            Order('default');
          }}>
            {t('Resetează', 'Reset')}
          </button>
        )}
      </div>
      <p className="small muted report-list-result" role="status">
        {shown.length} {t('din', 'of')} {rows.length} {t('deconturi afișate', 'reports shown')}
      </p>
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
                    aria-label={t('Deschide decontul', 'Open report') + ': ' + title(r)}
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
