import { formatMoney, parseFinancialJson, compare } from './decimal.mjs';
import { stateText } from './ReportDisplay';
import React, { useEffect, useState } from 'react';
import { api } from './api';
import { Field } from './Fields';
type Any = Record<string, any>;
export function Comments({ detail, t, run, refresh, allowed }: Any) {
  const [text, T] = useState(''),
    [line, L] = useState('');
  return (
    <section className="panel">
      <h2>{t('Observații pe decont și bonuri', 'Report and receipt comments')}</h2>
      {(detail.comments || []).map((c: Any) => (
        <article className="simple-row" key={c.id}>
          <div>
            <strong>
              {c.lineId
                ? t('Bon', 'Receipt') +
                  ' ' +
                  detail.report.lines.find((l: Any) => l.id === c.lineId)?.position
                : t('Decont', 'Report')}
            </strong>
            <p>{c.text}</p>
            <small>
              {c.actor} · {new Date(c.at).toLocaleString()} · {t('revizia', 'revision')}{' '}
              {c.revision}
            </small>
          </div>
        </article>
      ))}
      {allowed && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            run(async () => {
              await api('/reports/' + detail.report.id + '/comments', 'POST', {
                version: detail.report.version,
                lineId: line || null,
                text,
              });
              T('');
              await refresh();
            });
          }}
        >
          <Field name={t('Bonul comentat (opțional)', 'Receipt to comment on (optional)')}>
            <select value={line} onChange={(e) => L(e.target.value)}>
              <option value="">{t('Tot decontul', 'Whole report')}</option>
              {detail.report.lines.map((l: Any) => (
                <option key={l.id} value={l.id}>
                  {l.position}. {l.data.description}
                </option>
              ))}
            </select>
          </Field>
          <Field name={t('Comentariu', 'Comment')}>
            <textarea
              minLength={3}
              maxLength={2000}
              required
              value={text}
              onChange={(e) => T(e.target.value)}
            />
          </Field>
          <button className="secondary">{t('Adaugă observația', 'Add comment')}</button>
        </form>
      )}
    </section>
  );
}
export function ConfigurationImport({ kind, t, run, refresh }: Any) {
  const [preview, P] = useState<Any | null>(null),
    [reason, R] = useState('');
  useEffect(() => P(null), [kind]);
  return (
    <section className="panel">
      <h2>{t('Import nomenclator CSV / XLSX', 'Import master data CSV / XLSX')}</h2>
      <p>
        {t(
          'Verifică toate rândurile înainte de confirmare. Importul se aplică integral sau deloc.',
          'Review all rows before confirmation. Imports are applied entirely or not at all.',
        )}
      </p>
      <a href={'/api/configuration/import/template/' + encodeURIComponent(kind)}
        className="text-button">
        {t('Exemplu CSV pentru nomenclatorul ales', 'CSV example for selected master')}
      </a>
      <p className="small muted">{t(
        'Exemplele conțin anteturi și valori fictive. Nu importa numere complete de card sau parole.',
        'Examples include headers and illustrative values. Never import full card numbers or passwords.'
      )}</p>
      <Field name={t('Fișier import', 'Import file')}>
        <input
          type="file"
          accept=".csv,.xlsx"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file)
              run(async () => {
                const body = new FormData();
                body.append('file', file);
                body.append('kind', kind);
                P(await api('/configuration/import/preview', 'POST', body));
              });
          }}
        />
      </Field>
      {preview && (
        <>
          <p>
            {preview.count} {t('rânduri valide', 'valid rows')} · {preview.errors.length}{' '}
            {t('erori', 'errors')}
          </p>
          {preview.errors.map((e: Any) => (
            <p className="issue blocking" key={e.row}>
              {t('Rândul', 'Row')} {e.row}: {e.message}
            </p>
          ))}
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>{t('Cod', 'Code')}</th>
                  <th>{t('Denumire', 'Name')}</th>
                  <th>{t('Din', 'From')}</th>
                  <th>{t('Până la', 'To')}</th>
                </tr>
              </thead>
              <tbody>
                {preview.rows.map((r: Any, i: number) => (
                  <tr key={i}>
                    <td>{r.code}</td>
                    <td>{r.nameRo}</td>
                    <td>{r.from}</td>
                    <td>{r.to}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Field name={t('Motiv import', 'Import reason')}>
            <input minLength={3} value={reason} onChange={(e) => R(e.target.value)} />
          </Field>
          <button
            className="primary"
            disabled={preview.errors.length > 0 || !preview.count || reason.trim().length < 3}
            onClick={() =>
              run(async () => {
                await api('/configuration/import/confirm', 'POST', { rows: preview.rows, reason });
                P(null);
                await refresh();
              })
            }
          >
            {t('Confirmă importul integral', 'Confirm complete import')}
          </button>
        </>
      )}
    </section>
  );
}

function CostCenterOwnerImport({ t, run, refresh }: Any) {
  const [preview, Preview] = useState<Any | null>(null);
  const [reason, Reason] = useState('');
  return (
    <section className="cost-centre-owner-import" aria-label={t('Import responsabil manager', 'Manager owner import')}>
      <h3>{t('Importă responsabilii centrelor de cost', 'Import cost-centre owners')}</h3>
      <p className="small muted">{t(
        'Folosește coduri de centre de cost existente și username-uri de manageri activi. Importul este verificat integral înainte de confirmare.',
        'Use existing cost centre codes and active manager usernames. The entire import is validated before confirmation.'
      )}</p>
      <a className="text-button" href="/api/admin/cost-center-owners/template">
        {t('Descarcă exemplul CSV cu antet', 'Download sample CSV with headers')}
      </a>
      <Field name={t('Fișier responsabili CSV / XLSX', 'Owners CSV / XLSX file')}>
        <input type="file" accept=".csv,.xlsx" onChange={(e) => {
          const file = e.target.files?.[0];
          if (!file) return;
          run(async () => {
            const form = new FormData();
            form.append('file', file);
            Preview(await api('/admin/cost-center-owners/import/preview', 'POST', form));
          });
        }} />
      </Field>
      {preview && (
        <>
          <p role="status">{preview.count} {t('rânduri valide', 'valid rows')} · {preview.errors.length} {t('erori', 'errors')}</p>
          {preview.errors.map((e: Any) =>
            <p key={e.row} role="alert" className="issue blocking">{t('Rând', 'Row')} {e.row}: {e.message}</p>)}
          <div className="table-wrap">
            <table>
              <thead><tr><th>{t('Centru de cost', 'Cost centre')}</th><th>{t('Manager responsabil', 'Responsible manager')}</th></tr></thead>
              <tbody>{preview.rows.map((r: Any) =>
                <tr key={r.costCenterCode}><td>{r.costCenterCode}</td><td>{r.managerUsername}</td></tr>)}</tbody>
            </table>
          </div>
          <Field name={t('Motiv import', 'Import reason')}>
            <input value={reason} onChange={(e) => Reason(e.target.value)} minLength={3} />
          </Field>
          <button className="primary" type="button"
            disabled={!preview.count || preview.errors.length !== 0 || reason.trim().length < 3}
            onClick={() => run(async () => {
              await api('/admin/cost-center-owners/import/confirm', 'POST', {
                rows: preview.rows, reason,
              });
              Preview(null);
              Reason('');
              await refresh();
            })}>
            {t('Confirmă importul responsabililor', 'Confirm owner import')}
          </button>
        </>
      )}
    </section>
  );
}
export function Assignments({ t, run }: Any) {
  const [rows, R] = useState<Any[]>([]),
    [users, U] = useState<Any[]>([]),
    [costCentres, CC] = useState<Any[]>([]),
    [owners, Owners] = useState<Any[]>([]),
    [chosenOwners, ChooseOwners] = useState<Record<string, string>>({}),
    [ownerReason, OwnerReason] = useState(''),
    [form, F] = useState<Any>({
      userId: '',
      managerId: '',
      organizationCode: '',
      active: true,
      from: new Date().toISOString().slice(0, 10),
      to: '',
      reason: '',
    }),
    [end, E] = useState(new Date().toISOString().slice(0, 10));
  const load = async () => {
    const [assignments, users, owners, masters] = await Promise.all([
      api('/admin/assignments'), api('/admin/users'),
      api('/admin/cost-center-owners'), api('/configuration/masters'),
    ]);
    R(assignments);
    U(users);
    Owners(owners);
    const today = new Date().toISOString().slice(0, 10);
    CC(masters.filter((m: Any) =>
      m.kind === 'costCenters' && m.active && m.from <= today && (!m.to || m.to >= today))
      .sort((a: Any, b: Any) => a.code.localeCompare(b.code)));
  };
  useEffect(() => {
    run(load);
  }, []);
  return (
    <section className="panel">
      <h1>
        {t('Alocări și responsabili pe centre de cost', 'Assignments and cost-centre owners')}
      </h1>
      <p className="small muted">{t(
        'Responsabilul de aprobare se alege din proprietarul centrului de cost, nu din managerul organizațional. Pentru mai multe centre se însumează cheltuielile în RON pentru fiecare centru.',
        'Approval follows the cost centre owner, not the organisational manager. Multiple receipt amounts are summed in RON by cost centre.'
      )}</p>
      <h2>{t('Alocări organizaționale (independente de aprobare)', 'Organisation assignments (not approval routing)')}</h2>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          run(async () => {
            await api('/admin/assignments', 'POST', { ...form, to: form.to || null });
            await load();
          });
        }}
      >
        <div className="form-grid">
          {['userId', 'managerId'].map((k) => (
            <Field key={k} name={k === 'userId' ? t('Angajat', 'Employee') : t('Manager organizațional', 'Organisational manager')}>
              <select
                required
                value={form[k]}
                onChange={(e) => F({ ...form, [k]: e.target.value })}
              >
                <option value="">{t('Selectează', 'Select')}</option>
                {users
                  .filter((u) => u.active && (k === 'userId' || u.roles.includes('Manager')))
                  .map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name}
                    </option>
                  ))}
              </select>
            </Field>
          ))}
          {['organizationCode', 'from', 'to', 'reason'].map((k) => (
            <Field
              key={k}
              name={t(
                (
                  {
                    organizationCode: 'Cod organizație (opțional)',
                    from: 'Valabil din',
                    to: 'Valabil până la (opțional)',
                    reason: 'Motiv',
                  } as Any
                )[k],
                k,
              )}
            >
              <input
                type={['from', 'to'].includes(k) ? 'date' : 'text'}
                required={['from', 'reason'].includes(k)}
                value={form[k]}
                onChange={(e) => F({ ...form, [k]: e.target.value })}
              />
            </Field>
          ))}
          <Field name={t('Acces pe perioada alocării', 'Access during assignment')}>
            <select
              value={String(form.active)}
              onChange={(e) => F({ ...form, active: e.target.value === 'true' })}
            >
              <option value="true">{t('Activ', 'Active')}</option>
              <option value="false">{t('Dezactivat', 'Disabled')}</option>
            </select>
          </Field>
        </div>
        <button className="primary">{t('Creează alocarea', 'Create assignment')}</button>
      </form>
      <CostCenterOwnerImport t={t} run={run} refresh={load} />
      <section className="cost-centre-ownership" aria-label={t('Manageri responsabili de centrele de cost', 'Cost centre owner managers')}>
        <h2>{t('Responsabili pentru aprobarea deconturilor', 'Expense approval owners')}</h2>
        <p className="small muted">{t(
          'Fiecare centru de cost trebuie să aibă un manager activ cu rol Manager. Salvează modificările cu motiv; deconturile aflate în aprobare vor fi rerutate când responsabilul se schimbă.',
          'Every cost centre requires an active Manager owner. Record a reason when assigning one; pending approvals are rerouted when ownership changes.'
        )}</p>
        <Field name={t('Motivul modificării responsabilului', 'Reason for changing owner')}>
          <input type="text" value={ownerReason} required
            onChange={(e) => OwnerReason(e.target.value)} />
        </Field>
        <div className="cost-centre-owner-list">
          {costCentres.map((centre: Any) => {
            const saved = owners.find((o: Any) => o.costCenterCode === centre.code);
            const selected = chosenOwners[centre.code] ?? saved?.managerId ?? '';
            return (
              <div className="cost-centre-owner-row" key={centre.id}>
                <div>
                  <strong>{centre.code}</strong>
                  <p className="small muted">{t(centre.nameRo, centre.nameEn)}</p>
                </div>
                <Field name={t('Manager responsabil', 'Responsible manager')}>
                  <select value={selected}
                    aria-label={t('Manager pentru ', 'Manager for ') + centre.code}
                    onChange={(e) => ChooseOwners((old) => ({ ...old, [centre.code]: e.target.value }))}>
                    <option value="">{t('Alege managerul', 'Choose owner')}</option>
                    {users.filter((u) => u.active && u.roles.includes('Manager'))
                      .map((u) => (<option key={u.id} value={u.id}>{u.name}</option>))}
                  </select>
                </Field>
                <button type="button" className="secondary"
                  disabled={!selected || ownerReason.trim().length < 3 || selected === saved?.managerId}
                  onClick={() => run(async () => {
                    await api('/admin/cost-center-owners/' + encodeURIComponent(centre.code), 'PUT', {
                      costCenterCode: centre.code,
                      managerId: selected,
                      reason: ownerReason,
                    });
                    await load();
                    ChooseOwners((old) => {
                      const next = { ...old };
                      delete next[centre.code];
                      return next;
                    });
                  })}>
                  {t('Salvează responsabilul', 'Save owner')}
                </button>
              </div>
            );
          })}
        </div>
        {!costCentres.length && <p className="issue warning">{t(
          'Nu există centre de cost active. Configurează întâi nomenclatorul.',
          'There are no active cost centres. Configure the master list first.'
        )}</p>}
      </section>
      <Field name={t('Data închiderii unei alocări', 'Assignment end date')}>
        <input type="date" value={end} onChange={(e) => E(e.target.value)} />
      </Field>
      {rows.map((a) => (
        <div className="simple-row" key={a.id}>
          <span>
            {users.find((u) => u.id === a.userId)?.name} →{' '}
            {users.find((u) => u.id === a.managerId)?.name}
            <small>
              {a.from} → {a.to || '∞'} · {a.organizationCode}
            </small>
          </span>
          <button
            className="secondary"
            disabled={form.reason.length < 3}
            onClick={() =>
              run(async () => {
                await api('/admin/assignments/' + a.id + '/retire', 'POST', {
                  to: end,
                  reason: form.reason,
                });
                await load();
              })
            }
          >
            {t('Închide perioada', 'Close period')}
          </button>
        </div>
      ))}
    </section>
  );
}
export function FinanceWorklist({ t, run, open, masters = [], label = (m: Any) => m.nameRo }: Any) {
  const [rows, R] = useState<Any[]>([]),
    [filter, F] = useState<Any>({
      q: '',
      state: '',
      employee: '',
      manager: '',
      card: '',
      cardId: '',
      ready: '',
      supplier: '',
      age: '',
      exception: '',
      sort: 'age',
    }),
    [loaded, L] = useState(false),
    [compact, Compact] = useState(false);
  const readableState = (state: string) => {
    const names = stateText[state];
    return names ? t(names[0], names[1]) : state.replace(/_/g, ' ');
  };
  const exceptionLabels: Record<string, [string, string]> = {
    VENDOR_UNRESOLVED: ['Cont furnizor lipsă', 'Supplier account missing'],
    MISSING_VENDOR_ACCOUNT: ['Cont furnizor lipsă', 'Supplier account missing'],
    MISSING_COST_CENTER: ['Centru de cost lipsă', 'Cost centre missing'],
    OCR_REVIEW_REQUIRED: ['Bon de verificat', 'Receipt requires review'],
    MISSING_EVIDENCE: ['Document justificativ lipsă', 'Supporting document missing'],
  };
  const readableException = (code: string) => {
    const name = exceptionLabels[code];
    return name ? t(name[0], name[1]) : code.replace(/_/g, ' ').toLowerCase();
  };
  useEffect(() => {
    run(async () => {
      R(await api('/finance/queue'));
      const saved = await api('/preferences/finance-filters');
      if (saved.sort) {
        F(saved);
      }
      L(true);
    });
  }, []);
  const shown = rows
    .filter((x) => {
      const r = x.report;
      return (
        (!filter.state || r.state === filter.state) &&
        (!filter.ready || r.state === 'READY_FOR_ERP') &&
        (!filter.cardId || r.lines.some((l: Any) => l.data.cardId === filter.cardId)) &&
        (!filter.age || x.ageDays >= Number(filter.age)) &&
        (!filter.exception || x.exceptionCodes.includes(filter.exception)) &&
        [
          filter.q,
          [r.number, r.purpose].join(' '),
          filter.employee,
          x.employee || '',
          filter.manager,
          x.manager || '',
          filter.supplier,
          r.lines.map((l: Any) => l.data.supplier + ' ' + l.data.cui).join(' '),
        ].every(
          (v, i, a) => i % 2 === 1 || !v || a[i + 1].toLowerCase().includes(v.toLowerCase()),
        ) &&
        (!filter.card || r.lines.some((l: Any) => l.data.payment === 'CompanyCard'))
      );
    })
    .sort((a, b) =>
      filter.sort === 'amount'
        ? compare(b.report.total, a.report.total)
        : filter.sort === 'employee'
          ? (a.employee || '').localeCompare(b.employee || '')
          : b.ageDays - a.ageDays,
    );
  const ready = rows.filter((row) => row.report.state === 'READY_FOR_ERP').length;
  const exceptions = rows.filter((row) => row.exceptionCodes.length > 0).length;
  const activeFilters = Object.entries(filter).filter(
    ([key, value]) => key !== 'sort' && !!value,
  ).length;
  const resetFilters = () =>
    F({
      q: '', state: '', employee: '', manager: '', card: '', cardId: '',
      ready: '', supplier: '', age: '', exception: '', sort: 'age',
    });
  const filterNames: Record<string, [string, string]> = {
    q: ['Decont / scop', 'Report / purpose'],
    employee: ['Angajat', 'Employee'],
    manager: ['Manager', 'Manager'],
    supplier: ['Furnizor / CUI', 'Supplier / VAT ID'],
    age: ['Vechime minimă (zile)', 'Minimum age (days)'],
  };
  return (
    <section className={compact ? 'panel finance-queue-compact' : 'panel'}>
      <div className="section-heading">
        <h2>{t('Coada Finance și excepții', 'Finance queue and exceptions')}</h2>
        <button type="button" className="secondary"
          aria-pressed={compact} onClick={() => Compact((value) => !value)}>
          {compact ? t('Afișare confortabilă', 'Comfortable view') : t('Afișare compactă', 'Compact view')}
        </button>
      </div>
      <p className="muted">
        {t(
          'Verifică excepțiile și documentele înainte de orice acțiune contabilă.',
          'Check exceptions and supporting documents before any accounting action.',
        )}
      </p>
      <div className="finance-queue-summary" aria-label={t('Situația deconturilor', 'Report summary')}>
        <div><strong>{rows.length}</strong><span>{t('În coadă', 'In queue')}</span></div>
        <div><strong>{exceptions}</strong><span>{t('Cu excepții', 'With exceptions')}</span></div>
        <button type="button" className="secondary" onClick={() => F({ ...filter, ready: filter.ready ? '' : 'yes' })}>
          <strong>{ready}</strong>
          <span>{t('Gata pentru ERP', 'Ready for ERP')}{filter.ready ? ' ✓' : ''}</span>
        </button>
      </div>
      <section className="finance-filters">
        <h4>
          {t('Caută și filtrează deconturile', 'Search and filter reports')}
          {activeFilters > 0 ? ' · ' + activeFilters : ''}
        </h4>
      <div className="form-grid">
        {['q', 'employee', 'manager', 'supplier', 'age'].map((k) => (
          <Field
            key={k}
            name={t(filterNames[k][0], filterNames[k][1])}
          >
            <input
              type={k === 'age' ? 'number' : 'text'}
              value={filter[k]}
              onChange={(e) => F({ ...filter, [k]: e.target.value })}
            />
          </Field>
        ))}
        <Field name={t('Stare', 'Status')}>
          <select value={filter.state} onChange={(e) => F({ ...filter, state: e.target.value })}>
            <option value="">{t('Toate', 'All')}</option>
            {Array.from(new Set(rows.map((r) => r.report.state))).map((s) => (
              <option key={s} value={s}>{readableState(s)}</option>
            ))}
          </select>
        </Field>
        <Field name={t('Excepție', 'Exception')}>
          <select
            value={filter.exception}
            onChange={(e) => F({ ...filter, exception: e.target.value })}
          >
            <option value="">{t('Toate', 'All')}</option>
            {Array.from(new Set(rows.flatMap((r) => r.exceptionCodes))).map((s: any) => (
              <option key={s} value={s}>{readableException(s)}</option>
            ))}
          </select>
        </Field>
        <Field name={t('Sortare', 'Sort')}>
          <select value={filter.sort} onChange={(e) => F({ ...filter, sort: e.target.value })}>
            <option value="age">{t('Vechime', 'Age')}</option>
            <option value="amount">{t('Sumă', 'Amount')}</option>
            <option value="employee">{t('Angajat', 'Employee')}</option>
          </select>
        </Field>
        <Field name={t('Card specific', 'Specific card')}>
          <select
            value={filter.cardId || ''}
            onChange={(e) => F({ ...filter, cardId: e.target.value })}
          >
            <option value="">{t('Toate', 'All')}</option>
            {Array.from(
              new Set<string>(
                rows.flatMap((x) => x.report.lines.map((l: Any) => l.data.cardId).filter(Boolean)),
              ),
            ).map((id) => (
              <option key={id} value={id}>
                {masters.find((m: Any) => m.id === id)
                  ? label(masters.find((m: Any) => m.id === id))
                  : id}
              </option>
            ))}
          </select>
        </Field>
        <label>
          <input
            type="checkbox"
            checked={!!filter.ready}
            onChange={(e) => F({ ...filter, ready: e.target.checked ? 'yes' : '' })}
          />
          {t('Doar gata pentru ERP', 'ERP ready only')}
        </label>
        <label>
          <input
            type="checkbox"
            checked={!!filter.card}
            onChange={(e) => F({ ...filter, card: e.target.checked ? 'yes' : '' })}
          />
          {t('Doar card companie', 'Company card only')}
        </label>
      </div>
      <div className="actions">
        <button
          className="secondary"
          disabled={!loaded}
          onClick={() => run(() => api('/preferences/finance-filters', 'PUT', filter))}
        >
          {t('Salvează filtrele mele', 'Save my filters')}
        </button>
        <button type="button" className="secondary" onClick={resetFilters}>
          {t('Șterge filtrele active', 'Clear active filters')}
        </button>
      </div>
      </section>
      <p role="status" className="small muted">
        {shown.length} {t('deconturi afișate', 'reports shown')} {activeFilters
          ? ' · ' + activeFilters + ' ' + t('filtre active', 'active filters') : ''}
      </p>
      {shown.map((x) => (
        <button
          key={x.report.id}
          className="simple-row row-button"
          onClick={() => open(x.report.id)}
        >
          <span>
            <strong>
              {x.report.number} · {x.report.purpose}
            </strong>
            <small>
              {x.employee || t('Angajat nespecificat', 'Unspecified employee')}
              {' · '}{x.manager || t('Manager nespecificat', 'Unspecified manager')}
              {' · '}{x.ageDays} {t(x.ageDays === 1 ? 'zi' : 'zile', x.ageDays === 1 ? 'day' : 'days')}
            </small>
            <small className="finance-queue-money">
              {t('Total decont', 'Report total')}: {formatMoney(x.report.total ?? 0, t('ro-RO', 'en-GB'))} RON
              {' · '}{x.report.lines?.length || 0} {t('bonuri', 'receipts')}
            </small>
          </span>
          <span>
            <strong>{readableState(x.report.state)}</strong>
            <small>
              {x.exceptionCodes.length
                ? t('Necesită atenție: ', 'Needs attention: ') +
                  x.exceptionCodes.map(readableException).join(' · ')
                : t('Fără excepții detectate', 'No flagged exceptions')}
            </small>
            {x.exceptionCodes.length > 0 && (
              <small className="muted">
                {t('Coduri pentru audit', 'Audit codes')}: {x.exceptionCodes.join(', ')}
              </small>
            )}
          </span>
        </button>
      ))}
    </section>
  );
}
export function Reporting({ t, run, open }: Any) {
  const [rows, R] = useState<Any[]>([]),
    [group, G] = useState('month'),
    [q, Q] = useState(''),
    [savedReady, SavedReady] = useState(false),
    [filterFeedback, FilterFeedback] = useState('');
  const allowedGroups = ['month', 'employee', 'costCenter', 'category', 'supplier',
    'gl', 'project', 'cardId', 'state'];
  useEffect(() => {
    run(async () => {
      const [results, preferences] = await Promise.all([
        api('/reporting'),
        api('/preferences/reporting-filters'),
      ]);
      R(results);
      if (preferences && typeof preferences === 'object') {
        if (allowedGroups.includes(preferences.group)) G(preferences.group);
        if (typeof preferences.q === 'string') Q(preferences.q.slice(0, 300));
      }
      SavedReady(true);
    });
  }, []);
  const scoped = rows.filter((r) => JSON.stringify(r).toLowerCase().includes(q.toLowerCase()));
  const totals: Any = {};
  for (const row of scoped) {
    const key = String(row[group] || t('Nealocat', 'Unassigned'));
    totals[key] = (totals[key] || 0) + row.finalRon;
  }
  const download = () => {
    const keys = [
      'number',
      'employee',
      'date',
      'category',
      'supplier',
      'cui',
      'costCenter',
      'project',
      'cardId',
      'gl',
      'currency',
      'amount',
      'finalRon',
    ];
    const cell = (v: any) =>
      '"' +
      String(v ?? '')
        .replace(/^[=+@-]/, "'$&")
        .replaceAll('"', '""') +
      '"';
    const blob = new Blob(
      [
        '\ufeff' +
          [keys, ...scoped.map((r) => keys.map((k) => r[k]))]
            .map((r) => r.map(cell).join(','))
            .join('\r\n'),
      ],
      { type: 'text/csv' },
    );
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'cheltuieli.csv';
    a.click();
    URL.revokeObjectURL(url);
  };
  return (
    <section className="panel">
      <h1>{t('Raportare cheltuieli', 'Expense reporting')}</h1>
      <div className="form-grid">
        <Field name={t('Grupează după', 'Group by')}>
          <select value={group} onChange={(e) => G(e.target.value)}>
            {[
              'month',
              'employee',
              'costCenter',
              'category',
              'supplier',
              'gl',
              'project',
              'cardId',
              'state',
            ].map((k) => (
              <option key={k} value={k}>
                {t(
                  (
                    {
                      month: 'Lună',
                      employee: 'Angajat',
                      costCenter: 'Centru de cost',
                      category: 'Categorie',
                      supplier: 'Furnizor',
                      gl: 'Cont GL',
                      project: 'Proiect',
                      cardId: 'Card',
                      state: 'Stare',
                    } as Any
                  )[k],
                  k,
                )}
              </option>
            ))}
          </select>
        </Field>
        <Field name={t('Filtrează rezultatele', 'Filter results')}>
          <input value={q} onChange={(e) => Q(e.target.value)} />
        </Field>
      </div>
      <div className="actions reporting-preferences">
        <button type="button" className="secondary" disabled={!savedReady}
          onClick={() => run(async () => {
            await api('/preferences/reporting-filters', 'PUT', { group, q: q.slice(0, 300) });
            FilterFeedback(t('Filtrele tale de raportare au fost salvate.',
              'Your reporting filters have been saved.'));
          })}>
          {t('Memorează filtrele mele', 'Save my reporting filters')}
        </button>
        <button type="button" className="secondary" onClick={() => {
          G('month'); Q('');
          FilterFeedback(t('Filtrele afișate au fost resetate. Preferința salvată nu s-a schimbat.',
            'Visible filters reset. Your saved preference is unchanged.'));
        }}>
          {t('Resetează afișarea', 'Reset view')}
        </button>
        <button type="button" className="secondary" disabled={!scoped.length}
          onClick={download}>
          {t('Exportă rezultatele CSV', 'Export results CSV')}
        </button>
      </div>
      <p className="small muted" role="status">
        {scoped.length} {t('bonuri afișate', 'receipts shown')}
        {filterFeedback ? ' · ' + filterFeedback : ''}
      </p>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>{t('Grup', 'Group')}</th>
              <th>Total RON</th>
            </tr>
          </thead>
          <tbody>
            {Object.entries(totals).map(([key, value]: [string, any]) => (
              <tr key={key}>
                <td>{key}</td>
                <td>{formatMoney(value)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <section>
        <h4>{t('Detalii bonuri', 'Receipt details')}</h4>
        {scoped.map((r) => (
          <button className="simple-row row-button" key={r.lineId} onClick={() => open(r.reportId)}>
            {r.number} · {r.employee} · {r.date} · {r.amount} {r.currency}
          </button>
        ))}
      </section>
    </section>
  );
}
export function RevisionComparison({ detail, t, run }: Any) {
  const [revision, R] = useState(''),
    [snapshot, S] = useState<Any | null>(null);
  const keys = [
    'supplier',
    'cui',
    'invoice',
    'date',
    'country',
    'category',
    'description',
    'payment',
    'cardId',
    'costCenter',
    'project',
    'currency',
    'amount',
    'taxCurrency',
    'taxes',
    'accounting',
  ];
  const changes = (detail.report.lines || []).flatMap((line: Any) => {
    const old = snapshot?.lines?.find((l: Any) => l.id === line.id);
    if (!old)
      return [
        {
          line: line.position,
          field: t('Bon adăugat', 'Receipt added'),
          before: '—',
          after: line.data.description,
        },
      ];
    const data = parseFinancialJson(old.json);
    return keys
      .filter((key) => JSON.stringify(data[key] ?? null) !== JSON.stringify(line.data[key] ?? null))
      .map((key) => ({
        line: line.position,
        field: key,
        before: JSON.stringify(data[key] ?? null),
        after: JSON.stringify(line.data[key] ?? null),
      }));
  });
  return (
    <section className="panel">
      <h2>{t('Compară cu revizia trimisă', 'Compare against submitted revision')}</h2>
      <Field name={t('Revizie de referință', 'Reference revision')}>
        <select
          value={revision}
          onChange={(e) => {
            R(e.target.value);
            if (e.target.value)
              run(async () =>
                S(await api(`/reports/${detail.report.id}/snapshots/${e.target.value}`)),
              );
            else S(null);
          }}
        >
          <option value="">{t('Selectează', 'Select')}</option>
          {detail.snapshots.map((s: Any) => (
            <option key={s.revision} value={s.revision}>
              {t('Revizia', 'Revision')} {s.revision} · {s.hash.slice(0, 12)}
            </option>
          ))}
        </select>
      </Field>
      {snapshot && (
        <>
          <p>
            {t(
              'Comparație câmp cu câmp între revizia păstrată și datele actuale.',
              'Field-by-field comparison between retained revision and current data.',
            )}
          </p>
          {snapshot.purpose !== detail.report.purpose && (
            <p>
              {snapshot.purpose} → {detail.report.purpose}
            </p>
          )}
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>{t('Bon', 'Receipt')}</th>
                  <th>{t('Câmp', 'Field')}</th>
                  <th>{t('Trimis', 'Submitted')}</th>
                  <th>{t('Actual', 'Current')}</th>
                </tr>
              </thead>
              <tbody>
                {changes.map((c: Any, i: number) => (
                  <tr key={i}>
                    <td>{c.line}</td>
                    <td>{c.field}</td>
                    <td>{c.before}</td>
                    <td>{c.after}</td>
                  </tr>
                ))}
                {snapshot.lines
                  ?.filter(
                    (l: Any) => !detail.report.lines.some((current: Any) => current.id === l.id),
                  )
                  .map((l: Any) => (
                    <tr key={l.id}>
                      <td>{l.id}</td>
                      <td>{t('Bon arhivat', 'Receipt archived')}</td>
                      <td>{JSON.parse(l.json).description}</td>
                      <td>—</td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
          {!changes.length && (
            <p>{t('Nu există modificări ale câmpurilor active.', 'No active field changes.')}</p>
          )}
        </>
      )}
    </section>
  );
}
export function AuditSearch({ t, run }: Any) {
  const auditActionLabels: Record<string, [string, string]> = {
    Created: ['Decont creat', 'Report created'],
    LineAdded: ['Bon adăugat', 'Receipt added'],
    LineChanged: ['Bon modificat', 'Receipt edited'],
    Submitted: ['Decont trimis spre aprobare', 'Report submitted'],
    Returned: ['Decont returnat pentru corecții', 'Report returned'],
    approve: ['Decont aprobat', 'Report approved'],
    reject: ['Decont respins', 'Report rejected'],
    ERPExported: ['Export ERP generat', 'ERP export generated'],
    DocumentVerified: ['Document verificat', 'Document verified'],
    PostedBatchReopened: ['Postare redeschisă după reversare', 'Posting reopened after reversal'],
  };
  const humanAction = (action: string) => {
    const entry = auditActionLabels[action?.split(':')[0]];
    return entry ? t(entry[0], entry[1]) : (action || '').replace(/([a-z])([A-Z])/g, '$1 $2').replace(/_/g, ' ');
  };
  const [rows, R] = useState<Any[]>([]),
    [filter, F] = useState<Any>({ q: '', actor: '', action: '', from: '', to: '' });
  const query = () =>
    new URLSearchParams(
      Object.fromEntries(Object.entries(filter).filter(([, value]) => !!value)) as Record<
        string,
        string
      >,
    ).toString();
  const load = () => api('/audit?' + query()).then(R);
  useEffect(() => {
    run(load);
  }, []);
  return (
    <section className="panel">
      <h1>{t('Jurnal de audit', 'Audit log')}</h1>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          run(load);
        }}
      >
        <div className="form-grid">
          {['q', 'actor', 'action', 'from', 'to'].map((k) => (
            <Field
              key={k}
              name={t(
                (
                  {
                    q: 'Entitate / motiv',
                    actor: 'ID utilizator',
                    action: 'Acțiune',
                    from: 'Din data',
                    to: 'Până la data',
                  } as Any
                )[k],
                k,
              )}
            >
              <input
                type={['from', 'to'].includes(k) ? 'date' : 'text'}
                value={filter[k]}
                onChange={(e) => F({ ...filter, [k]: e.target.value })}
              />
            </Field>
          ))}
        </div>
        <div className="actions">
          <button className="primary">{t('Caută în audit', 'Search audit')}</button>
          <a className="secondary" href={'/api/audit/export?' + query()}>
            {t('Exportă auditul filtrat CSV', 'Export filtered audit CSV')}
          </a>
          <button type="button" className="secondary" onClick={() => {
            F({ q: '', actor: '', action: '', from: '', to: '' });
            run(() => api('/audit').then(R));
          }}>
            {t('Resetează și afișează toate', 'Reset and show all')}
          </button>
        </div>
      </form>
      <p className="small muted" role="status">
        {rows.length} {t('evenimente afișate', 'events shown')}.{' '}
        {t(
          'Maximum 1.000 evenimente afișate; exportul include maximum 50.000.',
          'Up to 1,000 displayed events; export includes up to 50,000.',
        )}
      </p>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>{t('Moment', 'Time')}</th>
              <th>{t('Utilizator', 'Actor')}</th>
              <th>{t('Acțiune', 'Action')}</th>
              <th>{t('Entitate', 'Entity')}</th>
              <th>{t('Detalii', 'Details')}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((a) => (
              <tr key={a.id}>
                <td>{new Date(a.at).toLocaleString()}</td>
                <td>{a.actor}</td>
                <td>
                  <strong>{humanAction(a.action)}</strong>
                  <small className="audit-raw-code">{t('Cod audit', 'Audit code')}: {a.action}</small>
                </td>
                <td>{a.entity}</td>
                <td>
                  <p>{a.reason || t('Fără motiv suplimentar', 'No additional reason')}</p>
                  <details className="audit-change-details">
                    <summary>{t('Vezi valorile înainte / după', 'Show before / after values')}</summary>
                    <div className="audit-change-grid">
                      <div>
                        <strong>{t('Înainte', 'Before')}</strong>
                        <pre className="ocr-text">{a.before || '—'}</pre>
                      </div>
                      <div>
                        <strong>{t('După', 'After')}</strong>
                        <pre className="ocr-text">{a.after || '—'}</pre>
                      </div>
                    </div>
                  </details>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

export function FinanceDashboard({ t, run, openQueue }: Any) {
  const [rows, R] = useState<Any[]>([]);
  useEffect(() => {
    run(async () => R(await api('/finance/queue')));
  }, []);
  const active = rows.filter((x) => !['POSTED', 'CLOSED'].includes(x.report.state));
  const groups = [
    ['vendor', t('Furnizori lipsă', 'Missing vendors')],
    ['vat_', t('Excepții TVA', 'VAT exceptions')],
    ['duplicate_', t('Duplicate', 'Duplicates')],
    ['reconciliation', t('Carduri nereconciliate', 'Unreconciled cards')],
    ['ready', t('Gata pentru ERP', 'ERP ready')],
    ['aged', t('Mai vechi de 7 zile', 'Older than 7 days')],
  ];
  return (
    <section className="panel">
      <h2>{t('Situația Finance', 'Finance overview')}</h2>
      <div className="stats">
        {groups.map(([code, label]) => {
          const count = active.filter((x) =>
            code === 'ready'
              ? x.report.state === 'READY_FOR_ERP'
              : code === 'aged'
                ? x.ageDays >= 7
                : x.exceptionCodes.some((v: string) => v.startsWith(code)),
          ).length;
          return (
            <button className="stat row-button" key={code} onClick={openQueue}>
              <span>{label}</span>
              <strong>{count}</strong>
            </button>
          );
        })}
      </div>
    </section>
  );
}

export function FinanceRouting({ report, t, run, refresh, reason }: Any) {
  const [users, U] = useState<Any[]>([]),
    [employee, E] = useState(report.employeeId);
  useEffect(() => {
    run(async () => U(await api('/directory')));
  }, []);
  return (
    <section className="panel">
      <h3>{t('Corectează angajatul decontului', 'Correct report employee')}</h3>
      <p>{t(
        'Finance poate corecta angajatul. Managerul de aprobare NU poate fi selectat manual: este proprietarul centrului de cost cu cea mai mare valoare cumulată a cheltuielilor.',
        'Finance can correct the employee. Approval manager cannot be chosen manually: it is the owner of the cost centre with the highest combined expense value.'
      )}</p>
      <div className="form-grid">
        <Field name={t('Angajat', 'Employee')}>
          <select value={employee} onChange={(e) => E(e.target.value)}>
            {users.filter((u) => u.roles.includes('Employee')).map((u) => (
              <option key={u.id} value={u.id}>{u.name} · {u.worker}</option>
            ))}
          </select>
        </Field>
        <Field name={t('Managerul atribuit anterior', 'Previously assigned approver')}
          note={t('Managerul va fi recalculat automat pe baza centrelor de cost ale bonurilor.',
                  'The approver will be recalculated automatically from receipt cost centres.')}>
          <input readOnly value={
            users.find((u) => u.id === report.managerId)?.name ||
            t('Nedisponibil', 'Unavailable')
          } />
        </Field>
      </div>
      <button className="secondary"
        disabled={reason.trim().length < 3 || employee === report.employeeId}
        onClick={() => run(async () => {
          await api('/finance/reports/' + report.id + '/routing', 'POST', {
            version: report.version, employeeId: employee, managerId: null, reason,
          });
          await refresh();
        })}>
        {t('Corectează angajatul și cere reaprobare', 'Correct employee & request reapproval')}
      </button>
    </section>
  );
}
