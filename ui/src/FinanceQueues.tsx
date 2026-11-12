import { useEffect, useState, type FormEvent } from 'react';
import { Download } from 'lucide-react';
import { api } from './api';
import { Field } from './Fields';

type Any = Record<string, any>;

function VendorResolutionRow({ vendor, t, run, load }: Any) {
  const [editing, Editing] = useState(false);
  const [account, Account] = useState('');
  const [from, From] = useState('');
  const [reason, Reason] = useState('');
  const [saving, Saving] = useState(false);

  const resolve = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const cleanAccount = account.trim();
    const cleanReason = reason.trim();
    if (saving || !cleanAccount || cleanReason.length < 3) return;
    const question = t('Asociezi contul ERP ', 'Assign ERP account ') +
      cleanAccount + t(' furnizorului ', ' to supplier ') +
      vendor.supplier + ' (CUI ' + vendor.cui + ')?';
    if (!window.confirm(question)) return;
    Saving(true);
    try {
      const success = await run(async () => {
        await api('/finance/vendors/' + vendor.id + '/resolve', 'POST', {
          account: cleanAccount,
          from: from || vendor.documentDate,
          reason: cleanReason,
        });
        await load();
        return true;
      });
      if (success) {
        Account('');
        From('');
        Reason('');
        Editing(false);
      }
    } finally {
      Saving(false);
    }
  };

  return (
    <div className="finance-action-row">
      <div className="simple-row">
        <span>
          <strong>{vendor.supplier}</strong>
          <small>CUI {vendor.cui}</small>
        </span>
        <button type="button" className="secondary"
          aria-expanded={editing}
          onClick={() => Editing(!editing)}>
          {editing ? t('Închide editorul', 'Close editor') : t('Asociază cont ERP', 'Assign ERP account')}
        </button>
      </div>
      {editing && (
        <form className="finance-inline-editor" onSubmit={resolve}>
          <p className="small">
            {t('Furnizor selectat: ', 'Selected supplier: ')}
            <strong>{vendor.supplier}</strong> · CUI {vendor.cui}
          </p>
          <div className="form-grid">
            <Field name={t('Cont furnizor ERP', 'ERP vendor account')}>
              <input required autoComplete="off" value={account}
                onChange={(event) => Account(event.target.value)} />
            </Field>
            <Field name={t('Valabil din (implicit: data primului bon)', 'Effective from (default: first receipt date)')}>
              <input type="date" value={from} onChange={(event) => From(event.target.value)} />
            </Field>
            <Field name={t('Motiv', 'Reason')}>
              <input required minLength={3} value={reason}
                onChange={(event) => Reason(event.target.value)} />
            </Field>
          </div>
          <p className="small muted">
            {t('Se va modifica doar acest furnizor. Verifică numele și contul înainte de confirmare.',
              'Only this supplier will be changed. Check the name and account before confirming.')}
          </p>
          <div className="actions">
            <button className="primary" type="submit"
              disabled={saving || !account.trim() || reason.trim().length < 3}>
              {saving ? t('Se salvează…', 'Saving…') : t('Asociază contul acestui furnizor', 'Assign this supplier account')}
            </button>
            <button className="secondary" type="button" disabled={saving}
              onClick={() => Editing(false)}>
              {t('Anulează', 'Cancel')}
            </button>
          </div>
        </form>
      )}
    </div>
  );
}

export function FinanceQueue({ t, run }: Any) {
  const [vendors, Vendors] = useState<Any[]>([]);
  const [accountHistory, AccountHistory] = useState<Any[]>([]);
  const load = async () => {
    Vendors(await api('/finance/vendors'));
    AccountHistory(await api('/finance/vendor-accounts'));
  };
  useEffect(() => { run(load); }, []);
  return (
    <section className="panel queue">
      <h2>{t('Furnizori de completat', 'Suppliers to resolve')}</h2>
      <p className="small muted">
        {t('Alege un furnizor pentru a-i completa contul ERP. Datele nu se transferă între furnizori.',
          'Choose a supplier to assign its ERP account. Values are never reused for other suppliers.')}
      </p>
      {vendors.filter((vendor) => vendor.state === 'Pending').map((vendor) => (
        <VendorResolutionRow key={vendor.id} vendor={vendor} t={t} run={run} load={load} />
      ))}
      {!vendors.some((vendor) => vendor.state === 'Pending') && (
        <p className="muted">{t('Nicio solicitare în așteptare.', 'No pending requests.')}</p>
      )}
      <section>
        <h4>{t('Istoric conturi furnizori ERP', 'Supplier ERP account history')}</h4>
        {accountHistory.map((account) => (
          <p key={account.id}>
            CUI {account.cui} · {account.account} · {account.from} → {account.to || '∞'}
          </p>
        ))}
      </section>
    </section>
  );
}

function ExportBatchRow({ batch, report, me, t, run, refresh, load }: Any) {
  const [reference, Reference] = useState('');
  const [voidReason, VoidReason] = useState('');
  const [reopenReason, ReopenReason] = useState('');
  const [reversal, Reversal] = useState('');
  const [saving, Saving] = useState(false);
  const canReopen = me.roles.includes('FinanceReopen');
  const status = batch.state === 'Posted'
    ? t('Postat în ERP', 'Posted in ERP')
    : batch.state === 'Exported'
      ? t('Export generat', 'Export generated')
      : batch.state;

  const execute = async (action: string, body: Any, question: string) => {
    if (saving || !window.confirm(question)) return;
    Saving(true);
    try {
      const success = await run(async () => {
        await api('/finance/batches/' + batch.id + '/' + action, 'POST', {
          version: report.version,
          ...body,
        });
        await refresh();
        await load();
        return true;
      });
      if (success) {
        Reference('');
        VoidReason('');
        ReopenReason('');
        Reversal('');
      }
    } finally {
      Saving(false);
    }
  };

  return (
    <article className="batch">
      <strong>{t('Ciclul', 'Cycle')} {batch.cycle} · {status}</strong>
      <small>{t('Identificator export: ', 'Export ID: ')}{batch.id}</small>
      <a className="text-button" href={'/api/batches/' + batch.id + '/xlsx'}>
        <Download size={15} aria-hidden="true" />
        {t('Descarcă XLSX', 'Download XLSX')}
      </a>
      {batch.state === 'Posted' && canReopen && (
        <form className="finance-inline-editor" onSubmit={(event) => {
          event.preventDefault();
          if (reopenReason.trim().length < 3 || reversal.trim().length < 3) return;
          execute('reopen', { reason: reopenReason.trim(), reversalReference: reversal.trim() },
            t('Redeschizi exportul din ciclul ', 'Reopen export from cycle ') +
            batch.cycle + t(' după reversarea în ERP cu referința ', ' after external ERP reversal reference ') +
            reversal.trim() + '?');
        }}>
          <p className="small muted">
            {t('Reversarea trebuie efectuată separat în ERP. Acest buton doar înregistrează redeschiderea în aplicație.',
              'Reversal must happen separately in the ERP. This action only records reopening here.')}
          </p>
          <div className="form-grid">
            <Field name={t('Referință reversare ERP', 'ERP reversal reference')}>
              <input required minLength={3} value={reversal} onChange={(event) => Reversal(event.target.value)} />
            </Field>
            <Field name={t('Motiv redeschidere', 'Reopen reason')}>
              <input required minLength={3} value={reopenReason}
                onChange={(event) => ReopenReason(event.target.value)} />
            </Field>
          </div>
          <button type="submit" className="secondary danger"
            disabled={saving || reopenReason.trim().length < 3 || reversal.trim().length < 3}>
            {t('Redeschide după reversare ERP', 'Reopen after ERP reversal')}
          </button>
        </form>
      )}
      {batch.state === 'Exported' && me.roles.includes('Finance') && (
        <>
          <form className="finance-inline-editor" onSubmit={(event) => {
            event.preventDefault();
            if (!reference.trim()) return;
            execute('posted', { reference: reference.trim() },
              t('Înregistrezi postarea exportului din ciclul ', 'Record posting of export from cycle ') +
              batch.cycle + t(' cu referința ERP ', ' with ERP reference ') + reference.trim() + '?');
          }}>
            <p className="small muted">
              {t('Confirmă doar după postarea efectivă în ERP; această acțiune nu postează automat în ERP.',
                'Confirm only after posting in the ERP; this action does not post automatically.')}
            </p>
            <Field name={t('Referință postare ERP', 'ERP posting reference')}>
              <input required value={reference} onChange={(event) => Reference(event.target.value)} />
            </Field>
            <button className="primary" type="submit" disabled={saving || !reference.trim()}>
              {t('Confirmă postarea', 'Confirm posting')}
            </button>
          </form>
          {canReopen && (
            <form className="finance-inline-editor" onSubmit={(event) => {
              event.preventDefault();
              if (voidReason.trim().length < 3) return;
              execute('void', { reason: voidReason.trim() },
                t('Anulezi exportul din ciclul ', 'Void export from cycle ') + batch.cycle +
                t('? Istoricul rămâne păstrat.', '? Its history will remain available.'));
            }}>
              <Field name={t('Motiv anulare', 'Void reason')}>
                <input required minLength={3} value={voidReason}
                  onChange={(event) => VoidReason(event.target.value)} />
              </Field>
              <button type="submit" className="secondary danger"
                disabled={saving || voidReason.trim().length < 3}>
                {t('Anulează exportul', 'Void export')}
              </button>
            </form>
          )}
        </>
      )}
    </article>
  );
}

export function BatchPanel({ report, me, t, run, refresh }: Any) {
  const [batches, Batches] = useState<Any[]>([]);
  const load = () => api('/finance/batches')
    .then((rows: Any[]) => Batches(rows.filter((batch) => batch.reportId === report.id)));
  useEffect(() => {
    if (me.roles.includes('Finance')) run(load);
  }, [report.version]);
  return (
    <div className="queue">
      <h3>{t('Exporturi salvate', 'Saved export batches')}</h3>
      {batches.map((batch) => (
        <ExportBatchRow key={batch.id} batch={batch} report={report} me={me}
          t={t} run={run} refresh={refresh} load={load} />
      ))}
      {!batches.length && (
        <p className="muted">{t('Nu există exporturi încă.', 'No export batches yet.')}</p>
      )}
    </div>
  );
}
