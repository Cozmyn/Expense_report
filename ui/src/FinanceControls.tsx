import React, { useEffect, useState } from 'react';
import { api } from './api';
import { Field } from './Fields';
import { formatMoney } from './decimal.mjs';
type Any = Record<string, any>;
export function TaxPolicyPanel({ t, run, me, masters }: Any) {
  const [data, D] = useState<Any>({ policies: [], decisions: [] }),
    [reason, R] = useState('');
  const company = masters?.find(
    (m: Any) => m.kind === 'policies' && m.code === 'company' && m.active,
  );
  const [form, F] = useState<Any>({
    company: company ? String(JSON.parse(company.json).cui).replace(/^RO/, '') : 'DEFAULT',
    countryGroup: 'RO',
    documentKind: '*',
    companyCui: 'PRESENT',
    category: '*',
    from: new Date().toISOString().slice(0, 10),
    to: '',
    priority: 10,
    inputVatRequired: true,
    allowZeroBase: true,
    treatment: 'STANDARD',
    authority: '',
    deductibility: 'CATEGORY_MAPPING',
  });
  const load = () => api('/finance/tax-policies').then(D);
  useEffect(() => {
    run(load);
  }, []);
  const set = (key: string, value: any) => F({ ...form, [key]: value });
  return (
    <section>
      <h4>{t('Politici TVA și aprobare', 'VAT policies & approval')}</h4>
      <p>
        {t(
          'Ciorna nu schimbă calculele. Alt utilizator Finance trebuie să o aprobe. Cursurile pe bon nu cer o nouă aprobare de manager.',
          'Drafts do not change calculations. A different Finance user must approve them. Receipt FX changes do not require another manager approval.',
        )}
      </p>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          run(async () => {
            await api('/finance/tax-policies', 'POST', {
              rule: { ...form, to: form.to || null, priority: Number(form.priority) },
              reason,
            });
            await load();
          });
        }}
      >
        <div className="form-grid">
          <Field name={t('Companie', 'Company')}>
            <input required value={form.company} onChange={(e) => set('company', e.target.value)} />
          </Field>
          <Field name={t('Tip document', 'Document kind')}>
            <select value={form.documentKind} onChange={(e) => set('documentKind', e.target.value)}>
              <option value="*">{t('Orice tip', 'Any kind')}</option>
              <option value="Receipt">{t('Bon fiscal', 'Receipt')}</option>
              <option value="InvoiceWithCui">{t('Factură cu CUI', 'Invoice with VAT ID')}</option>
              <option value="InvoiceWithoutCui">
                {t('Factură fără CUI', 'Invoice without VAT ID')}
              </option>
              {masters
                .filter(
                  (m: Any) =>
                    m.kind === 'documentTypes' &&
                    !['Receipt', 'InvoiceWithCui', 'InvoiceWithoutCui'].includes(m.code),
                )
                .map((m: Any) => (
                  <option key={m.id} value={m.code}>
                    {t(m.nameRo, m.nameEn)}
                  </option>
                ))}
            </select>
          </Field>
          <Field name={t('Categorie', 'Category')}>
            <select value={form.category} onChange={(e) => set('category', e.target.value)}>
              <option value="*">{t('Orice categorie', 'Any category')}</option>
              {masters
                .filter((m: Any) => m.kind === 'expenseTypes')
                .map((m: Any) => (
                  <option key={m.id} value={m.code}>
                    {t(m.nameRo, m.nameEn)}
                  </option>
                ))}
            </select>
          </Field>
          <Field name={t('Țara documentului', 'Document country')}>
            <select value={form.countryGroup} onChange={(e) => set('countryGroup', e.target.value)}>
              <option value="RO">{t('România', 'Romania')}</option>
              <option value="FOREIGN">{t('Străinătate', 'Foreign')}</option>
              <option value="*">{t('Oricare', 'Any')}</option>
            </select>
          </Field>
          <Field name={t('CUI companie pe bon', 'Company VAT ID printed')}>
            <select value={form.companyCui} onChange={(e) => set('companyCui', e.target.value)}>
              <option value="PRESENT">{t('Prezent', 'Present')}</option>
              <option value="ABSENT">{t('Absent', 'Absent')}</option>
              <option value="*">{t('Oricare', 'Any')}</option>
            </select>
          </Field>
          {[
            ['from', t('Valabil din', 'Effective from')],
            ['to', t('Valabil până la', 'Effective to')],
          ].map(([key, name]) => (
            <Field key={key} name={name}>
              <input
                type="date"
                required={key === 'from'}
                value={form[key]}
                onChange={(e) => set(key, e.target.value)}
              />
            </Field>
          ))}
          <Field name={t('Prioritate', 'Priority')}>
            <input
              type="number"
              min="0"
              max="1000"
              value={form.priority}
              onChange={(e) => set('priority', e.target.value)}
            />
          </Field>
          <Field name={t('Tratament TVA', 'VAT treatment')}>
            <select
              value={form.treatment}
              onChange={(e) =>
                F({
                  ...form,
                  treatment: e.target.value,
                  inputVatRequired: e.target.value === 'STANDARD',
                })
              }
            >
              {[
                ['STANDARD', t('Standard', 'Standard')],
                ['EXEMPT', t('Scutit', 'Exempt')],
                ['OUTSIDE_SCOPE', t('În afara sferei', 'Outside scope')],
                ['NOT_PRESENT', t('TVA neprezentat', 'VAT not present')],
              ].map(([v, label]) => (
                <option key={v} value={v}>
                  {label}
                </option>
              ))}
            </select>
          </Field>
          <Field name={t('TVA tipărit obligatoriu', 'Printed VAT required')}>
            <select
              value={String(form.inputVatRequired)}
              onChange={(e) => set('inputVatRequired', e.target.value === 'true')}
            >
              <option value="true">{t('Da', 'Yes')}</option>
              <option value="false">{t('Nu', 'No')}</option>
            </select>
          </Field>
          <Field name={t('Permite bază 0%', 'Allow zero-rated base')}>
            <select
              value={String(form.allowZeroBase)}
              onChange={(e) => set('allowZeroBase', e.target.value === 'true')}
            >
              <option value="true">{t('Da, documentată', 'Yes, documented')}</option>
              <option value="false">{t('Nu', 'No')}</option>
            </select>
          </Field>
          <Field name={t('Referință autoritate', 'Authority reference')}>
            <input
              required
              value={form.authority}
              onChange={(e) => set('authority', e.target.value)}
            />
          </Field>
        </div>
        <Field name={t('Motiv politică', 'Policy reason')}>
          <input required minLength={3} value={reason} onChange={(e) => R(e.target.value)} />
        </Field>
        <button className="primary">{t('Salvează ciorna politicii', 'Save policy draft')}</button>
      </form>
      {data.policies.map((p: Any) => {
        const rule = JSON.parse(p.json),
          approved = data.decisions.some((d: Any) => d.policyId === p.id && d.action === 'APPROVE'),
          retired = data.decisions.some((d: Any) => d.policyId === p.id && d.action === 'RETIRE');
        return (
          <div className="expense" key={p.id}>
            <strong>
              {rule.company} · {rule.countryGroup} · {rule.documentKind} · {rule.category}
            </strong>
            <p>
              {rule.from} → {rule.to || '∞'} · {rule.authority}
            </p>
            <p>
              {retired
                ? t('Închisă', 'Retired')
                : approved
                  ? t('Aprobată', 'Approved')
                  : t('Ciornă', 'Draft')}{' '}
              · {p.author}
            </p>
            {!approved && !retired && (
              <button
                className="secondary"
                disabled={p.author === me?.id || reason.trim().length < 3}
                onClick={() =>
                  run(async () => {
                    await api('/finance/tax-policies/' + p.id + '/approve', 'POST', { reason });
                    await load();
                  })
                }
              >
                {t('Aprobă politica', 'Approve policy')}
              </button>
            )}
            {approved && !retired && (
              <button
                className="secondary danger"
                disabled={reason.trim().length < 3}
                onClick={() =>
                  run(async () => {
                    await api('/finance/tax-policies/' + p.id + '/retire', 'POST', { reason });
                    await load();
                  })
                }
              >
                {t('Închide politica', 'Retire policy')}
              </button>
            )}
            {data.decisions
              .filter((d: Any) => d.policyId === p.id)
              .map((d: Any) => (
                <p key={d.id}>
                  {new Date(d.at).toLocaleString()} · {d.actor} · {d.reason}
                </p>
              ))}
          </div>
        );
      })}
    </section>
  );
}
export function ResetPanel({ report, t, run, refresh, editable }: Any) {
  const [data, D] = useState<Any>({ resets: [], restores: [] }),
    [confirmation, C] = useState(''),
    [reason, R] = useState('');
  const load = () => api(`/reports/${report.id}/resets`).then(D);
  useEffect(() => {
    run(load);
  }, [report.id, report.version]);
  const body = { version: report.version, confirmation, reason };
  const valid = confirmation === report.number && reason.trim().length >= 3;
  return (
    <section className="panel">
      <h4>{t('Golire și restaurare', 'Reset & restore')}</h4>
      <p>
        {t(
          'Golirea arhivează bonurile și păstrează fișierele și istoricul. Restaurarea este disponibilă doar când decontul este încă gol.',
          'Reset archives receipts and retains files and history. Restore is available only while the report is still empty.',
        )}
      </p>
      {editable && (
        <>
          <Field name={t('Confirmă numărul decontului', 'Confirm report number')}>
            <input value={confirmation} onChange={(e) => C(e.target.value)} />
          </Field>
          <Field name={t('Motiv golire/restaurare', 'Reset/restore reason')}>
            <input value={reason} onChange={(e) => R(e.target.value)} />
          </Field>
          <button
            className="secondary danger"
            disabled={!valid || report.lines.length === 0}
            onClick={() =>
              run(async () => {
                await api(`/reports/${report.id}/reset`, 'POST', body);
                await refresh();
              })
            }
          >
            {t('Golește reversibil decontul', 'Reset report reversibly')}
          </button>
        </>
      )}
      {data.resets.map((reset: Any) => (
        <div className="simple-row" key={reset.id}>
          <span>
            {new Date(reset.at).toLocaleString()} · {reset.reason}
          </span>
          {editable && !data.restores.some((r: Any) => r.resetId === reset.id) && (
            <button
              className="secondary"
              disabled={!valid || report.lines.length > 0}
              onClick={() =>
                run(async () => {
                  await api(`/reports/${report.id}/resets/${reset.id}/restore`, 'POST', body);
                  await refresh();
                })
              }
            >
              {t('Restaurează bonurile', 'Restore receipts')}
            </button>
          )}
        </div>
      ))}
    </section>
  );
}
export function CalculationVersions({ report, line, t, run }: Any) {
  const [data, D] = useState<Any>({ versions: [], page: 1, total: 0 });
  const load = (page = 1) =>
    api(`/reports/${report.id}/lines/${line.id}/calculation-history?page=${page}`).then(D);
  useEffect(() => {
    run(() => load());
  }, [report.version, line.id]);
  const labels: Any = {
    'preview.finalRon': ['Final RON', 'Final RON'],
    'preview.estimatedRon': ['Estimare RON', 'RON estimate'],
    'preview.roundingRon': ['Rotunjire RON', 'RON rounding'],
    'original.amount': ['Total document', 'Document total'],
    'original.currency': ['Monedă', 'Currency'],
    'original.vatTreatment': ['Tratament TVA', 'VAT treatment'],
  };
  return (
    <section>
      <h4>{t('Compară versiunile calculate', 'Compare calculated versions')}</h4>
      {data.versions.map((v: Any) => (
        <article className="expense" key={v.id}>
          <strong>
            {new Date(v.at).toLocaleString()} · {v.actor}
          </strong>
          <p>
            {t('Total confirmat', 'Confirmed total')}: {formatMoney(v.summary.finalRon)} RON
          </p>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>{t('Câmp', 'Field')}</th>
                  <th>{t('Înainte', 'Before')}</th>
                  <th>{t('După', 'After')}</th>
                </tr>
              </thead>
              <tbody>
                {v.differences
                  .filter((d: Any) => !d.field.startsWith('mappings') && !d.field.includes('Hash'))
                  .map((d: Any, i: number) => (
                    <tr key={i}>
                      <td>{labels[d.field] ? t(...labels[d.field]) : d.field}</td>
                      <td>{d.before ?? '—'}</td>
                      <td>{d.after ?? '—'}</td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
          {!v.differences.length && (
            <p>{t('Prima versiune acceptată.', 'First accepted version.')}</p>
          )}
        </article>
      ))}
      {data.total > 25 && (
        <div className="actions">
          <button
            className="secondary"
            disabled={data.page <= 1}
            onClick={() => run(() => load(data.page - 1))}
          >
            {t('Anterior', 'Previous')}
          </button>
          <span>{data.page}</span>
          <button
            className="secondary"
            disabled={data.page * 25 >= data.total}
            onClick={() => run(() => load(data.page + 1))}
          >
            {t('Următor', 'Next')}
          </button>
        </div>
      )}
      {!data.total && <p>{t('Nicio versiune acceptată.', 'No accepted version.')}</p>}
    </section>
  );
}
export function FinanceAcceptancePanel({ t, run, masters }: Any) {
  const [data, D] = useState<Any | null>(null),
    [checks, C] = useState<Any>({
      totalsChecked: false,
      sourceChecked: false,
      roundingChecked: false,
      erpChecked: false,
    }),
    [reason, R] = useState('');
  const company = masters?.find(
    (m: Any) => m.kind === 'policies' && m.code === 'company' && m.active,
  );
  const [companyCode, Company] = useState(
    company ? String(JSON.parse(company.json).cui).replace(/^RO/, '') : 'DEFAULT',
  );
  const load = () => api('/finance/acceptance').then(D);
  useEffect(() => {
    run(load);
  }, []);
  return (
    <section>
      <h4>{t('Acceptanță Finance și ERP', 'Finance & ERP acceptance')}</h4>
      <p>
        {t(
          'Confirmă doar verificări efectuate efectiv pe documente și în ERP. Testele automate nu înlocuiesc această acceptanță. Modificarea configurării impune o nouă verificare.',
          'Confirm only checks actually performed against documents and ERP. Automated tests do not replace this acceptance. Configuration changes require a fresh review.',
        )}
      </p>
      <Field name={t('Companie verificată', 'Reviewed company')}>
        <input value={companyCode} onChange={(e) => Company(e.target.value)} />
      </Field>
      {[
        ['totalsChecked', t('Am reprodus bazele și totalurile', 'I reproduced bases and totals')],
        [
          'sourceChecked',
          t('Am verificat documentele justificative', 'I checked supporting documents'),
        ],
        [
          'roundingChecked',
          t('Am verificat politica de rotunjire și FX', 'I checked rounding and FX policy'),
        ],
        [
          'erpChecked',
          t('Am verificat mapările și importul ERP', 'I checked mappings and ERP import'),
        ],
      ].map(([key, label]) => (
        <label className="check-line" key={key}>
          <input
            type="checkbox"
            checked={checks[key]}
            onChange={(e) => C({ ...checks, [key]: e.target.checked })}
          />
          {label}
        </label>
      ))}
      <Field name={t('Referință acceptanță', 'Acceptance reference')}>
        <input value={reason} onChange={(e) => R(e.target.value)} />
      </Field>
      {data?.issues.map((i: Any, k: number) => (
        <p className="issue blocking" key={k}>
          {t(i.ro, i.en)}
        </p>
      ))}
      <button
        className="primary"
        disabled={
          !data ||
          data.issues.length > 0 ||
          !Object.values(checks).every(Boolean) ||
          reason.trim().length < 3 ||
          !companyCode
        }
        onClick={() =>
          run(async () => {
            await api('/finance/acceptance', 'POST', {
              ...checks,
              company: companyCode,
              configurationHash: data!.configurationHash,
              reason,
            });
            await load();
          })
        }
      >
        {t('Înregistrează acceptanța Finance', 'Record Finance acceptance')}
      </button>
      {data?.records.map((r: Any) => (
        <div className="simple-row" key={r.id}>
          <span>
            {new Date(r.at).toLocaleString()} · {r.actor} · {r.reason}
          </span>
          <strong>
            {r.configurationHash === data.configurationHash
              ? t('Configurare curentă', 'Current configuration')
              : t('Configurare anterioară', 'Previous configuration')}
          </strong>
        </div>
      ))}
      {data && !data.records.length && (
        <p>
          {t(
            'Nu există o acceptanță Finance înregistrată.',
            'No Finance acceptance has been recorded.',
          )}
        </p>
      )}
    </section>
  );
}
