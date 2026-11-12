import BankCharges from './BankCharges';
import { minimum, compare, negate } from './decimal.mjs';
import React, { useEffect, useRef, useState } from 'react';
import { api } from './api';
import { Field } from './Fields';
type Any = Record<string, any>;
export default function Reconciliation({ t, masters, label, run, open }: Any) {
  const [data, D] = useState<Any>({ transactions: [], matches: [] }),
    [lines, L] = useState<Any[]>([]),
    [suggestions, S] = useState<Any[]>([]),
    [card, C] = useState(''),
    [csv, Csv] = useState(''),
    [headers, H] = useState<string[]>([]),
    [columns, Cols] = useState<Any>({}),
    [preview, P] = useState<Any | null>(null),
    [tx, Tx] = useState(''),
    [line, Line] = useState(''),
    [amount, A] = useState(''),
    [undoReasons, UndoReasons] = useState<Record<string, string>>({}),
    [matching, Matching] = useState(false);
  const allocationRef = useRef<HTMLElement>(null);
  const amountInputRef = useRef<HTMLInputElement>(null);
  const keys = ['reference', 'date', 'merchant', 'amount', 'currency', 'settledRon'];
  const names: Any = {
    reference: 'Referință',
    date: 'Data',
    merchant: 'Comerciant',
    amount: 'Sumă',
    currency: 'Monedă',
    settledRon: 'Sumă decontată RON',
  };
  const load = async () => {
    D(await api('/finance/transactions'));
    L(await api('/finance/lines'));
    S(await api('/finance/suggestions'));
  };
  useEffect(() => {
    run(load);
  }, []);
  useEffect(() => {
    P(null);
    if (card) run(async () => Cols(await api('/preferences/bank-columns:' + card)));
  }, [card]);
  useEffect(() => P(null), [csv, columns]);
  const bank = { cardId: card, csv, columns };
  const selected = lines.find((l) => l.id === line);
  const transaction = data.transactions.find((entry: Any) => entry.id === tx);
  const transactionSuggestion = suggestions.find((entry: Any) => entry.transaction.id === tx);
  const remainingTransaction = transactionSuggestion?.remaining ?? transaction?.remaining ?? null;
  const enteredAmount = amount.trim();
  const validAmount = /^-?\d+(?:\.\d+)?$/.test(enteredAmount) &&
    compare(enteredAmount, '0') !== 0;
  const exceeds = validAmount && (
    (remainingTransaction != null &&
      compare(enteredAmount.startsWith('-') ? negate(enteredAmount) : enteredAmount,
        String(remainingTransaction).startsWith('-') ? negate(String(remainingTransaction)) : String(remainingTransaction)) > 0) ||
    (selected && compare(enteredAmount.startsWith('-') ? negate(enteredAmount) : enteredAmount,
      String(selected.remaining).startsWith('-') ? negate(String(selected.remaining)) : String(selected.remaining)) > 0)
  );
  return (
    <>
      <div className="page-heading">
        <div>
          <h1>{t('Carduri și tranzacții', 'Cards and transactions')}</h1>
          <p>
            {t(
              'Import cu mapare de coloane; confirmă alocări parțiale sau multiple după verificare.',
              'Import with column mapping; confirm partial or multiple allocations after review.',
            )}
          </p>
        </div>
      </div>
      <section className="panel">
        <h2>{t('Import extras CSV / XLSX', 'Import CSV / XLSX statement')}</h2>
        <Field name={t('Card companie', 'Company card')}>
          <select value={card} onChange={(e) => C(e.target.value)}>
            <option value="">{t('Selectează', 'Select')}</option>
            {masters
              .filter((m: Any) => m.kind === 'cards' && m.active)
              .map((m: Any) => (
                <option key={m.id} value={m.id}>
                  {label(m)} · {JSON.parse(m.json).lastFour}
                </option>
              ))}
          </select>
        </Field>
        <Field name={t('Selectează extrasul', 'Browse statement')}>
          <input
            type="file"
            accept=".csv,.xlsx"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file)
                run(async () => {
                  const form = new FormData();
                  form.append('file', file);
                  const result = await api('/finance/transactions/file', 'POST', form);
                  H(result.headers);
                  Csv(result.csv);
                  P(null);
                });
            }}
          />
        </Field>
        {headers.length > 0 && (
          <div className="form-grid">
            {keys.map((k) => (
              <Field key={k} name={t(names[k], k)}>
                <select
                  value={columns[k] || k}
                  onChange={(e) => Cols({ ...columns, [k]: e.target.value })}
                >
                  {headers.map((h) => (
                    <option key={h}>{h}</option>
                  ))}
                </select>
              </Field>
            ))}
          </div>
        )}
        <Field
          name={t('Date CSV (sau lipește valorile)', 'CSV data (or paste values)')}
          note={t(
            'Dată YYYY-MM-DD; valori numerice cu punct zecimal.',
            'Date YYYY-MM-DD; numeric values with decimal point.',
          )}
        >
          <textarea className="csv-preview" value={csv} onChange={(e) => Csv(e.target.value)} />
        </Field>
        <div className="actions">
          <button
            className="secondary"
            disabled={!card || !csv}
            onClick={() =>
              run(async () => P(await api('/finance/transactions/preview', 'POST', bank)))
            }
          >
            {t('Verifică importul', 'Preview import')}
          </button>
          <button
            className="secondary"
            disabled={!card}
            onClick={() => run(() => api('/preferences/bank-columns:' + card, 'PUT', columns))}
          >
            {t('Salvează maparea cardului', 'Save card column mapping')}
          </button>
          <button
            className="primary"
            disabled={!preview || preview.duplicates.length > 0 || preview.errors?.length > 0}
            onClick={() =>
              run(async () => {
                await api('/finance/transactions/import', 'POST', bank);
                await load();
                P(null);
                Csv('');
              })
            }
          >
            {t('Confirmă importul', 'Confirm import')}
          </button>
        </div>
        {preview && (
          <>
            <p>
              {preview.count} {t('tranzacții', 'transactions')} · {preview.duplicates.length}{' '}
              {t('duplicate', 'duplicates')}
            </p>
            {preview.errors?.map((e: Any) => (
              <p className="issue blocking" key={e.row}>
                {t('Rând', 'Row')} {e.row}: {e.message}
              </p>
            ))}
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>{t('Referință', 'Reference')}</th>
                    <th>{t('Data', 'Date')}</th>
                    <th>{t('Comerciant', 'Merchant')}</th>
                    <th>{t('Sumă', 'Amount')}</th>
                    <th>RON</th>
                  </tr>
                </thead>
                <tbody>
                  {preview.rows.map((x: Any) => (
                    <tr key={x.reference}>
                      <td>{x.reference}</td>
                      <td>{x.date}</td>
                      <td>{x.merchant}</td>
                      <td>
                        {x.amount} {x.currency}
                      </td>
                      <td>{x.settledRon}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </section>
      <BankCharges t={t} run={run} masters={masters} refresh={load} />
      <section className="panel">
        <h2>{t('Sugestii de reconciliere', 'Reconciliation suggestions')}</h2>
        {suggestions
          .filter((s) => compare(s.remaining, '0') !== 0)
          .map((s) => (
            <div className="transaction" key={s.transaction.id}>
              <h3>
                {s.transaction.reference} · {s.transaction.merchant} · {s.remaining}{' '}
                {s.transaction.currency}
              </h3>
              {s.ambiguous && (
                <p className="issue warning">
                  {t(
                    'Mai multe potriviri au același scor. Verifică documentele și selectează manual.',
                    'Several matches have equal scores. Check the sources and select manually.',
                  )}
                </p>
              )}
              {s.candidates.map((c: Any) => (
                <div className="simple-row" key={c.id}>
                  <span>
                    {c.score}/100 · {c.amount} {s.transaction.currency}
                  </span>
                  <button
                    className="secondary"
                    onClick={() => {
                      Tx(s.transaction.id);
                      Line(c.id);
                      const available = lines.find((l) => l.id === c.id)?.remaining ?? c.amount;
                      A(
                        compare(s.remaining, '0') < 0
                          ? negate(minimum(negate(s.remaining), negate(available)))
                          : minimum(s.remaining, available),
                      );
                      allocationRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                      requestAnimationFrame(() => amountInputRef.current?.focus());

                    }}
                  >
                    {t('Selectează pentru verificare', 'Select for review')}
                  </button>
                  <button className="text-button" onClick={() => open(c.reportId)}>
                    {t('Deschide decontul', 'Open report')}
                  </button>
                </div>
              ))}
            </div>
          ))}
      </section>
      <section className="panel" ref={allocationRef} aria-label={t('Verifică și confirmă alocarea', 'Review and confirm allocation')}>
        <h2>{t('Alocare manuală / parțială', 'Manual / partial allocation')}</h2>
        <p className="small muted">
          {t('1. Alege tranzacția · 2. Alege bonul eligibil · 3. Verifică suma și confirmă.',
            '1. Choose a transaction · 2. Choose an eligible receipt · 3. Check the amount and confirm.')}
        </p>
        <div className="form-grid">
          <Field name={t('Tranzacție', 'Transaction')}>
            <select
              value={tx}
              onChange={(e) => {
                Tx(e.target.value);
                Line('');
                A('');
              }}
            >
              <option value="">{t('Selectează', 'Select')}</option>
              {data.transactions.map((x: Any) => (
                <option key={x.id} value={x.id}>
                  {x.reference} · {x.merchant} · {x.amount} {x.currency}
                </option>
              ))}
            </select>
          </Field>
          <Field name={t('Bon eligibil', 'Eligible receipt')}>
            <select value={line} onChange={(e) => { Line(e.target.value); A(''); }}>
              <option value="">{t('Selectează', 'Select')}</option>
              {lines
                .filter((l) => {
                  const transaction = data.transactions.find((x: Any) => x.id === tx);
                  return (
                    compare(l.remaining, '0') !== 0 &&
                    (!transaction ||
                      (l.data.cardId === transaction.cardId &&
                        l.data.currency === transaction.currency))
                  );
                })
                .map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.reportNumber} · {l.position}. {l.data.description} · {l.remaining}{' '}
                    {l.data.currency}
                  </option>
                ))}
            </select>
          </Field>
          <Field name={t('Sumă alocată', 'Allocated amount')}>
            <input ref={amountInputRef} type="number" step="any" value={amount}
              onChange={(e) => A(e.target.value)} aria-invalid={!!exceeds} />
          </Field>
        </div>
        {transaction && (
          <div className="reconciliation-review" role="status">
            <h3>{t('Verifică alocarea înainte de confirmare', 'Review before confirming')}</h3>
            <p><strong>{t('Tranzacție', 'Transaction')}:</strong> {transaction.reference} · {transaction.merchant}
              {' · '}{transaction.amount} {transaction.currency}</p>
            <p><strong>{t('Disponibil tranzacție', 'Transaction unallocated balance')}:</strong> {remainingTransaction === null
              ? t('Verifică în extras', 'Check statement') : remainingTransaction + ' ' + transaction.currency}</p>
            <p><strong>{t('Bon ales', 'Selected receipt')}:</strong> {selected
              ? selected.reportNumber + ' · ' + selected.position + '. ' + selected.data.description
              : t('Alege un bon eligibil', 'Choose an eligible receipt')}</p>
            {selected && <p><strong>{t('Disponibil bon', 'Receipt unallocated balance')}:</strong> {selected.remaining} {selected.data.currency}</p>}
            <p><strong>{t('Aloci acum', 'Allocating now')}:</strong> {enteredAmount || '—'} {transaction.currency}</p>
            {exceeds && <p className="issue blocking" role="alert">
              {t('Suma depășește valoarea încă nealocată. Verifică suma sau alege alt bon.',
                'Amount exceeds an unallocated balance. Check the amount or choose another receipt.')}
            </p>}
          </div>
        )}
        <button
          className="primary"
          disabled={matching || !selected || !tx || !validAmount || !!exceeds}
          onClick={() =>
            run(async () => {
              if (!selected || !transaction || !validAmount || exceeds) return;
              const message = t('Confirmi alocarea de ', 'Confirm allocation of ') +
                enteredAmount + ' ' + transaction.currency + t(' din tranzacția ', ' from transaction ') +
                transaction.reference + t(' către bonul ', ' to receipt ') +
                selected.reportNumber + ' / ' + selected.position + '?';
              if (!window.confirm(message)) return;
              Matching(true);
              try {
                const detail = await api('/reports/' + selected.reportId);
                await api('/finance/matches', 'POST', {
                  reportId: selected.reportId,
                  version: detail.report.version,
                  lineId: line,
                  transactionId: tx,
                  amount: enteredAmount,
                });
                await load();
                A('');
              } finally {
                Matching(false);
              }
            })
          }
        >
          {matching ? t('Se confirmă…', 'Confirming…') : t('Confirmă alocarea', 'Confirm allocation')}
        </button>
      </section>
      <section className="panel">
        <h2>{t('Alocări confirmate', 'Confirmed allocations')}</h2>
         {data.matches.map((m: Any) => {
          const receipt = lines.find((l) => l.id === m.lineId);
          return (
            <div className="simple-row" key={m.id}>
              <span>
                {data.transactions.find((x: Any) => x.id === m.transactionId)?.reference} →{' '}
                {receipt?.reportNumber || m.lineId}
                <small>
                  {m.amount} · {m.ron} RON
                </small>
              </span>
              <div className="reconciliation-undo">
                <Field name={t('Motiv anulare pentru această alocare', 'Undo reason for this allocation')}>
                  <input value={undoReasons[m.id] || ''} minLength={3}
                    onChange={(e) => UndoReasons((old) => ({ ...old, [m.id]: e.target.value }))} />
                </Field>
              <button
                className="secondary danger"
                disabled={!receipt || (undoReasons[m.id] || '').trim().length < 3}
                onClick={() =>
                  run(async () => {
                    if (!receipt) return;
                    const note = (undoReasons[m.id] || '').trim();
                    if (!window.confirm(t('Anulezi această alocare? Motiv: ', 'Undo this allocation? Reason: ') + note)) return;
                    const detail = await api('/reports/' + receipt.reportId);
                    await api('/finance/matches/' + m.id + '/undo', 'POST', {
                      version: detail.report.version,
                      reason: note,
                    });
                    UndoReasons((old) => ({ ...old, [m.id]: '' }));
                    await load();
                  })
                }
              >
                {t('Anulează alocarea', 'Undo allocation')}
              </button>
              </div>
            </div>
          );
        })}
      </section>
    </>
  );
}
