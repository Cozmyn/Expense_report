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
export function ReportList({ rows, t, state, open }: Any) {
  const [q, Q] = useState(''),
    [filter, F] = useState('');
  const shown = rows.filter(
    (r: Any) =>
      (r.number + ' ' + r.purpose + ' ' + (r.employeeName || '') + ' ' + (r.managerName || ''))
        .toLowerCase()
        .includes(q.toLowerCase()) &&
      (!filter || r.state === filter),
  );
  return (
    <>
      <div className="list-toolbar">
        <div className="search">
          <Search size={18} />
          <input
            placeholder={t('Caută decont sau scop…', 'Search report or purpose…')}
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
      <div className="table-wrap">
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
                    {r.purpose || t('Decont fără titlu', 'Untitled report')}
                    <small>
                      {r.number} · {r.employeeName} · {r.managerName} ·{' '}
                      {r.ageDays ??
                        Math.floor((Date.now() - new Date(r.created).getTime()) / 86400000)}{' '}
                      {t('zile', 'days')}
                    </small>
                  </button>
                </td>
                <td>
                  <span className={'badge ' + r.state.toLowerCase()}>{state(r.state)}</span>
                </td>
                <td className="muted">{new Date(r.updated).toLocaleDateString()}</td>
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
      {!shown.length && (
        <div className="empty">
          <Receipt size={32} />
          <h3>{t('Niciun decont aici, deocamdată.', 'No reports here yet.')}</h3>
          <p>
            {t(
              'Deconturile care corespund selecției vor apărea aici.',
              'Reports matching your selection will appear here.',
            )}
          </p>
        </div>
      )}
    </>
  );
}
