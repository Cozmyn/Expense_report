import { useEffect, useState } from 'react';
import { Download } from 'lucide-react';
import { api } from './api';
import { Field } from './Fields';

type Any = Record<string, any>;

export function FinanceQueue({ t, run }: Any) {
  const [vendors, V] = useState<Any[]>([]),
    [accountHistory, AH] = useState<Any[]>([]),
    [account, A] = useState(''),
    [from, F] = useState(''),
    [reason, R] = useState('');
  const load = async () => {
    V(await api('/finance/vendors'));
    AH(await api('/finance/vendor-accounts'));
  };
  useEffect(() => {
    run(load);
  }, []);
  return (
    <section className="panel queue">
      <h2>{t('Furnizori de completat', 'Suppliers to resolve')}</h2>
      <div className="form-grid">
        {' '}
        <Field name={t('Cont furnizor ERP', 'ERP vendor account')}>
          <input value={account} onChange={(e) => A(e.target.value)} />
        </Field>
        <Field
          name={t(
            'Valabil din (implicit: data primului bon)',
            'Effective from (default: first receipt date)',
          )}
        >
          <input type="date" value={from} onChange={(e) => F(e.target.value)} />
        </Field>
        <Field name={t('Motiv', 'Reason')}>
          <input value={reason} onChange={(e) => R(e.target.value)} />
        </Field>
      </div>
      {vendors
        .filter((v) => v.state === 'Pending')
        .map((v) => (
          <div className="simple-row" key={v.id}>
            <span>
              <strong>{v.supplier}</strong>
              <small>CUI {v.cui}</small>
            </span>
            <button
              className="secondary"
              disabled={!account || reason.length < 3}
              onClick={() =>
                run(async () => {
                  await api('/finance/vendors/' + v.id + '/resolve', 'POST', {
                    account,
                    from: from || v.documentDate,
                    reason,
                  });
                  await load();
                })
              }
            >
              {t('Alocă și rezolvă', 'Assign & resolve')}
            </button>
          </div>
        ))}
      {!vendors.some((v) => v.state === 'Pending') && (
        <p className="muted">{t('Nicio solicitare în așteptare.', 'No pending requests.')}</p>
      )}
      <section>
        <h4>{t('Istoric conturi furnizori ERP', 'Supplier ERP account history')}</h4>
        {accountHistory.map((a) => (
          <p key={a.id}>
            CUI {a.cui} · {a.account} · {a.from} → {a.to || '∞'}
          </p>
        ))}
      </section>
    </section>
  );
}
export function BatchPanel({ report, me, t, run, refresh }: Any) {
  const [batches, B] = useState<Any[]>([]),
    [reference, R] = useState(''),
    [reason, Reason] = useState(''),
    [reversal, RV] = useState('');
  const load = () =>
    api('/finance/batches').then((xs: Any[]) => B(xs.filter((b) => b.reportId === report.id)));
  useEffect(() => {
    if (me.roles.includes('Finance')) run(load);
  }, [report.version]);
  return (
    <div className="queue">
      <h3>{t('Exporturi salvate', 'Saved export batches')}</h3>
      {batches.map((b) => (
        <div className="batch" key={b.id}>
          <strong>
            {t('Ciclul', 'Cycle')} {b.cycle} · {b.state}
          </strong>
          <small>{b.id}</small>
          <a className="text-button" href={'/api/batches/' + b.id + '/xlsx'}>
            <Download size={15} />
            {t('Descarcă XLSX', 'Download XLSX')}
          </a>
          {b.state === 'Posted' && me.roles.includes('FinanceReopen') && (
            <div className="form-grid">
              <Field name={t('Referință reversare ERP', 'ERP reversal reference')}>
                <input value={reversal} onChange={(e) => RV(e.target.value)} />
              </Field>
              <Field name={t('Motiv redeschidere', 'Reopen reason')}>
                <input value={reason} onChange={(e) => Reason(e.target.value)} />
              </Field>
              <button
                className="secondary danger"
                disabled={reason.length < 3 || reversal.length < 3}
                onClick={() =>
                  run(async () => {
                    await api('/finance/batches/' + b.id + '/reopen', 'POST', {
                      version: report.version,
                      reason,
                      reversalReference: reversal,
                    });
                    await refresh();
                    await load();
                  })
                }
              >
                {t('Redeschide după reversare ERP', 'Reopen after ERP reversal')}
              </button>
            </div>
          )}
          {b.state === 'Exported' && me.roles.includes('Finance') && (
            <div className="form-grid">
              <Field name={t('Referință postare ERP', 'ERP posting reference')}>
                <input value={reference} onChange={(e) => R(e.target.value)} />
              </Field>
              <button
                className="primary"
                disabled={!reference}
                onClick={() =>
                  run(async () => {
                    await api('/finance/batches/' + b.id + '/posted', 'POST', {
                      version: report.version,
                      reference,
                    });
                    await refresh();
                    await load();
                  })
                }
              >
                {t('Confirmă postarea', 'Confirm posting')}
              </button>
              {me.roles.includes('FinanceReopen') && (
                <>
                  <Field name={t('Motiv anulare', 'Void reason')}>
                    <input value={reason} onChange={(e) => Reason(e.target.value)} />
                  </Field>
                  <button
                    className="secondary danger"
                    disabled={reason.length < 3}
                    onClick={() =>
                      run(async () => {
                        await api('/finance/batches/' + b.id + '/void', 'POST', {
                          version: report.version,
                          reason,
                        });
                        await refresh();
                        await load();
                      })
                    }
                  >
                    {t('Anulează exportul', 'Void export')}
                  </button>
                </>
              )}
            </div>
          )}
        </div>
      ))}
      {!batches.length && (
        <p className="muted">{t('Nu există exporturi încă.', 'No export batches yet.')}</p>
      )}
    </div>
  );
}
