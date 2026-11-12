import { useEffect, useState } from 'react';
import { Plus, ArrowUpRight, Upload, Download } from 'lucide-react';
import { api } from './api';
import { Field } from './Fields';
import { money } from './ReportDisplay';
import { minimum, compare, masterJson } from './decimal.mjs';
import { ConfigurationImport } from './PlatformPages';
import EmployeeImport from './EmployeeImport';

type Any = Record<string, any>;
type Master = Any;
const today = () => new Date().toISOString().slice(0, 10);

const masterKinds: Record<string, [string, string]> = {
  costCenters: ['Centre de cost', 'Cost centres'],
  projects: ['Proiecte', 'Projects'],
  expenseTypes: ['Tipuri de cheltuieli', 'Expense types'],
  cards: ['Carduri companie', 'Company cards'],
  vat: ['Cote și mapări TVA', 'VAT rates & mappings'],
  fx: ['Cursuri interne', 'Internal FX rates'],
  accounting: ['Mapări contabile', 'Accounting mappings'],
  suppliers: ['Furnizori', 'Suppliers'],
  fieldRules: ['Reguli de câmpuri', 'Field rules'],
  erpSettings: ['Setări export ERP', 'ERP export settings'],
  countries: ['Țări', 'Countries'],
  documentTypes: ['Tipuri document', 'Document types'],
  organizationUnits: ['Unități organizaționale', 'Organization units'],
  policies: ['Politici interne', 'Internal policies'],
  notificationRules: ['Notificări și remindere', 'Notifications and reminders'],
};
const masterFields: Record<string, string[]> = {
  costCenters: ['erpCode'],
  projects: ['erpCode'],
  expenseTypes: [],
  cards: ['lastFour', 'issuer', 'currency'],
  vat: ['rate', 'salesTaxGroup', 'itemTaxGroup', 'deductible50Code', 'nonDeductible50Code'],
  fx: ['ronRate'],
  accounting: ['mainAccount', 'localAccount', 'subAccount', 'vehicle', 'repair'],
  suppliers: ['vendorAccount'],
  fieldRules: [
    'field',
    'state',
    'priority',
    'country',
    'documentType',
    'category',
    'payment',
    'currency',
  ],
  countries: [],
  documentTypes: [],
  organizationUnits: ['parentCode'],
  policies: [
    'cui',
    'vatZeroTolerance',
    'maxFileMb',
    'maxReportMb',
    'requireCardMatchBeforeApproval',
    'requireReapprovalOnPersonalIncrease',
  ],
  notificationRules: [
    'inApp',
    'email',
    'afterHours',
    'repeatHours',
    'escalateHours',
    'escalationRole',
  ],
  erpSettings: [
    'roundingAccount',
    'roundingLocal',
    'filenamePattern',
    'approvalReference',
    'clearingAccount',
    'clearingLocal',
    'vatAccount',
    'vatLocal',
    'otherCarNonDedAccount',
    'repairNonDedAccount',
    'repairNonDedLocal',
    'exportDate',
    'businessUnit',
    'objective',
    'site',
    'productionLine',
    'exportCurrency',
    'documentNumber',
    'dueDate',
  ],
};
const fieldNames: Record<string, [string, string]> = {
  filenamePattern: [
    'Șablon nume fișier ({number}, {cycle}, {date})',
    'Filename pattern ({number}, {cycle}, {date})',
  ],
  parentCode: ['Cod organizație părinte', 'Parent organization code'],
  cui: ['CUI companie (cod company)', 'Company VAT ID (code company)'],
  vatZeroTolerance: ['Toleranță bază 0% (RON)', '0% base tolerance (RON)'],
  maxFileMb: ['Limită fișier (MB)', 'File limit (MB)'],
  maxReportMb: ['Limită decont (MB)', 'Report limit (MB)'],
  requireReapprovalOnPersonalIncrease: [
    'Reaprobare la creșterea rambursării',
    'Reapprove reimbursement increases',
  ],
  requireCardMatchBeforeApproval: ['Reconciliere înainte de manager', 'Reconcile before manager'],
  inApp: ['Notificări în aplicație', 'In-app notifications'],
  email: ['Notificări email', 'Email notifications'],
  afterHours: ['Reminder după ore', 'Reminder after hours'],
  repeatHours: ['Repetare după ore', 'Repeat after hours'],
  escalateHours: ['Escalare după ore', 'Escalate after hours'],
  escalationRole: ['Rol escalare', 'Escalation role'],
  erpCode: ['Cod ERP', 'ERP code'],
  ownerId: ['ID titular card', 'Card owner ID'],
  lastFour: ['Ultimele 4 cifre', 'Last four digits'],
  issuer: ['Emitent card', 'Card issuer'],
  rate: ['Cotă TVA (%)', 'VAT rate (%)'],
  salesTaxGroup: ['Grup TVA', 'Sales tax group'],
  itemTaxGroup: ['Cod TVA', 'Item tax group'],
  deductible50Code: ['Cod deductibil 50%', '50% deductible code'],
  nonDeductible50Code: ['Cod nedeductibil 50%', '50% non-deductible code'],
  ronRate: ['Valoarea în RON pentru 1 unitate', 'RON value per currency unit'],
  mainAccount: ['Cont GL', 'GL account'],
  localAccount: ['Cont local', 'Local account'],
  subAccount: ['Subcont', 'Sub account'],
  vehicle: ['Cheltuială auto', 'Vehicle expense'],
  repair: ['Reparații auto', 'Vehicle repairs'],
  vendorAccount: ['Cont furnizor ERP', 'ERP vendor account'],
  field: ['Câmp vizat', 'Target field'],
  state: ['Obligatoriu / opțional / neaplicabil', 'Required / optional / not applicable'],
  priority: ['Prioritate', 'Priority'],
  country: ['Condiție: țară', 'Condition: country'],
  documentType: ['Condiție: tip document', 'Condition: document type'],
  category: ['Condiție: categorie', 'Condition: category'],
  payment: ['Condiție: plată', 'Condition: payment'],
  currency: ['Condiție: monedă', 'Condition: currency'],
  approvalReference: ['Referința aprobării regulilor', 'Rule approval reference'],
  clearingAccount: ['Cont decontare GL', 'Clearing GL account'],
  roundingAccount: ['Cont rotunjire', 'Rounding account'],
  roundingLocal: ['Cont local rotunjire', 'Rounding local account'],
  clearingLocal: ['Cont local decontare', 'Clearing local account'],
  vatAccount: ['Cont GL TVA', 'VAT GL account'],
  vatLocal: ['Cont local TVA', 'VAT local account'],
  otherCarNonDedAccount: ['GL auto nedeductibil', 'Non-deductible vehicle GL'],
  repairNonDedAccount: ['GL reparații nedeductibile', 'Non-deductible repair GL'],
  repairNonDedLocal: ['Cont local reparații nedeductibile', 'Non-deductible repair local account'],
  exportDate: ['Data ERP (opțional)', 'ERP date (optional)'],
  businessUnit: ['Unitate organizațională (opțional)', 'Business unit (optional)'],
  objective: ['Obiectiv (opțional)', 'Objective (optional)'],
  site: ['Locație (opțional)', 'Site (optional)'],
  productionLine: ['Linie producție (opțional)', 'Production line (optional)'],
  exportCurrency: ['Monedă ERP (opțional)', 'ERP currency (optional)'],
  documentNumber: ['Număr document ERP (opțional)', 'ERP document number (optional)'],
  dueDate: ['Data scadenței (opțional)', 'Due date (optional)'],
};
export function MasterPage({ t, run, refresh }: Any) {
  const [rows, R] = useState<Any[]>([]),
    [kind, K] = useState('costCenters'),
    [form, F] = useState<Any>({
      code: '',
      nameRo: '',
      nameEn: '',
      from: today(),
      to: '',
      active: true,
      reason: '',
    }),
    [values, V] = useState<Any>({}),
    [retireDate, RD] = useState(today()),
    [people, People] = useState<Any[]>([]);
  const load = () => api('/configuration/masters').then(R);
  useEffect(() => {
    run(async () => {
      await load();
      People(await api('/directory'));
    });
  }, []);
  const set = (k: string, v: any) => F({ ...form, [k]: v });
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">{t('CONFIGURARE', 'CONFIGURATION')}</span>
          <h1>{t('Nomenclatoare și reguli', 'Master data & rules')}</h1>
          <p>
            {t(
              'Versiuni cu perioade de valabilitate. Închide versiunea anterioară înainte de a crea una nouă.',
              'Effective-dated versions. Close the preceding version before creating a new one.',
            )}
          </p>
        </div>
      </div>
      <ConfigurationImport
        kind={kind}
        t={t}
        run={run}
        refresh={async () => {
          await load();
          await refresh();
        }}
      />
      <section className="panel">
        <Field name={t('Nomenclator', 'Master type')}>
          <select
            value={kind}
            onChange={(e) => {
              K(e.target.value);
              V({});
            }}
          >
            {Object.entries(masterKinds).map(([k, n]) => (
              <option key={k} value={k}>
                {t(...n)}
              </option>
            ))}
          </select>
        </Field>
        {['policies', 'notificationRules'].includes(kind) && (
          <p className="receipt-hint">
            {kind === 'policies'
              ? t(
                  'Coduri: company (CUI), uploads (limite), vat (toleranță 0%), workflow (reconciliere). Completează câmpurile aferente codului ales.',
                  'Codes: company (VAT ID), uploads (limits), vat (0% tolerance), workflow (reconciliation). Complete the fields for your selected code.',
                )
              : t(
                  'Cod default pentru canale generale, codul evenimentului pentru excepții sau reminders pentru intervale și escalare.',
                  'Use default for general channels, the event code for exceptions, or reminders for intervals and escalation.',
                )}
          </p>
        )}
        <form
          onSubmit={(e) => {
            e.preventDefault();
            run(async () => {
              const data = { ...values };
              if (kind === 'fieldRules') {
                data.conditions = Object.fromEntries(
                  ['country', 'documentType', 'category', 'payment', 'currency']
                    .filter((k) => data[k])
                    .map((k) => [k, data[k]]),
                );
                for (const key of ['country', 'documentType', 'category', 'payment', 'currency'])
                  delete data[key];
              }
              await api('/configuration/masters', 'POST', {
                ...form,
                kind,
                to: form.to || null,
                json: masterJson(data),
              });
              await load();
              await refresh();
            });
          }}
        >
          <div className="form-grid">
            {[
              ['code', t('Cod', 'Code')],
              ['nameRo', 'Denumire în română'],
              ['nameEn', 'English name'],
              ['from', t('Valabil din', 'Effective from')],
              ['to', t('Valabil până la (opțional)', 'Effective to (optional)')],
            ].map(([key, name]) => (
              <Field key={key} name={name}>
                <input
                  type={['from', 'to'].includes(key) ? 'date' : 'text'}
                  value={form[key]}
                  required={key !== 'to'}
                  onChange={(e) => set(key, e.target.value)}
                />
              </Field>
            ))}
            {masterFields[kind].map((key) => (
              <Field key={key} name={t(...fieldNames[key])}>
                {key === 'ownerId' ? (
                  <select
                    required
                    value={values[key] || ''}
                    onChange={(e) => V({ ...values, [key]: e.target.value })}
                  >
                    <option value="">{t('Selectează titularul', 'Select card owner')}</option>
                    {people.map((u) => (
                      <option key={u.id} value={u.id}>
                        {u.name} · {u.worker}
                      </option>
                    ))}
                  </select>
                ) : [
                    'vehicle',
                    'repair',
                    'inApp',
                    'email',
                    'requireCardMatchBeforeApproval',
                    'requireReapprovalOnPersonalIncrease',
                  ].includes(key) ? (
                  <select
                    value={String(values[key] ?? key === 'requireReapprovalOnPersonalIncrease')}
                    onChange={(e) => V({ ...values, [key]: e.target.value === 'true' })}
                  >
                    <option value="false">{t('Nu', 'No')}</option>
                    <option value="true">{t('Da', 'Yes')}</option>
                  </select>
                ) : key === 'state' ? (
                  <select
                    value={values[key] || ''}
                    required
                    onChange={(e) => V({ ...values, [key]: e.target.value })}
                  >
                    <option value="">{t('Selectează', 'Select')}</option>
                    {['REQUIRED', 'OPTIONAL', 'NOT_APPLICABLE'].map((s) => (
                      <option key={s}>{s}</option>
                    ))}
                  </select>
                ) : (
                  <input
                    value={values[key] ?? ''}
                    type={
                      [
                        'rate',
                        'ronRate',
                        'priority',
                        'vatZeroTolerance',
                        'maxFileMb',
                        'maxReportMb',
                        'afterHours',
                        'repeatHours',
                        'escalateHours',
                      ].includes(key)
                        ? 'number'
                        : 'text'
                    }
                    step="any"
                    onChange={(e) =>
                      V({
                        ...values,
                        [key]:
                          key === 'ronRate'
                            ? e.target.value
                            : [
                                  'rate',
                                  'priority',
                                  'vatZeroTolerance',
                                  'maxFileMb',
                                  'maxReportMb',
                                  'afterHours',
                                  'repeatHours',
                                  'escalateHours',
                                ].includes(key)
                              ? Number(e.target.value)
                              : e.target.value,
                      })
                    }
                  />
                )}
              </Field>
            ))}
          </div>
          <Field name={t('Motivul modificării', 'Reason for change')}>
            <input
              required
              minLength={3}
              value={form.reason}
              onChange={(e) => set('reason', e.target.value)}
            />
          </Field>
          <button className="primary">
            <Plus size={16} />
            {t('Creează versiune', 'Create version')}
          </button>
        </form>
        <div className="queue">
          <Field name={t('Închidere perioadă la', 'Close period on')}>
            <input type="date" value={retireDate} onChange={(e) => RD(e.target.value)} />
          </Field>
          {rows
            .filter((m) => m.kind === kind)
            .map((m) => (
              <div className="simple-row" key={m.id}>
                <span>
                  <strong>
                    {m.code} · {m.nameRo}
                  </strong>
                  <small>
                    {m.from} → {m.to || '∞'}
                  </small>
                </span>
                <button
                  className="secondary"
                  disabled={form.reason.length < 3}
                  onClick={() =>
                    run(async () => {
                      await api('/configuration/masters/' + m.id + '/retire', 'POST', {
                        to: retireDate,
                        reason: form.reason,
                      });
                      await load();
                      await refresh();
                    })
                  }
                >
                  {t('Închide perioada', 'Close period')}
                </button>
              </div>
            ))}
        </div>
      </section>
    </>
  );
}
export function UsersPage({ t, run }: Any) {
  const [users, U] = useState<Any[]>([]),
    [costCentres, CC] = useState<Any[]>([]),
    [form, F] = useState<Any>({
      username: '',
      name: '',
      email: '',
      worker: '',
      costCenter: '',
      managerId: '',
      approvalMode: 'CostCenter',
      fixedApproverId: '',
      active: true,
      roles: ['Employee'],
      password: '',
      reason: '',
    }),
    [id, I] = useState(''),
    [userSearch, UserSearch] = useState(''),
    [userRole, UserRole] = useState(''),
    [userActivity, UserActivity] = useState('all'),
    [resetPassword, ResetPassword] = useState(''),
    [resetReason, ResetReason] = useState('');
  const load = async () => {
    const [users, masters] = await Promise.all([
      api('/admin/users'), api('/configuration/masters'),
    ]);
    U(users);
    const day = new Date().toISOString().slice(0, 10);
    CC(masters.filter((m: Any) =>
      m.kind === 'costCenters' && m.active && m.from <= day && (!m.to || m.to >= day)));
  };
  useEffect(() => {
    run(load);
  }, []);
  const set = (k: string, v: any) => F({ ...form, [k]: v });
  const visibleUsers = users.filter((user) => {
    const text = [user.userName, user.name, user.email, user.worker, user.costCenter]
      .filter(Boolean).join(' ').toLocaleLowerCase();
    return (!userSearch.trim() || text.includes(userSearch.trim().toLocaleLowerCase())) &&
      (!userRole || user.roles?.includes(userRole)) &&
      (userActivity === 'all' || (userActivity === 'active' ? user.active : !user.active));
  });
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">{t('ADMINISTRARE', 'ADMINISTRATION')}</span>
          <h1>{t('Utilizatori și acces', 'Users & access')}</h1>
          <p>
            {t(
              'Centrele de cost se atribuie fiecărui angajat. Managerul care aprobă decontul se stabilește automat din proprietarul centrului de cost dominant.',
              'Assign each employee a cost centre. The approval manager is chosen automatically from the owner of the highest-total cost centre.',
            )}
          </p>
        </div>
      </div>
      <EmployeeImport t={t} run={run} refresh={load} />
      <section className="panel">
        <h2>
          {id ? t('Editează contul', 'Edit account') : t('Creează un cont', 'Create an account')}
        </h2>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            run(async () => {
              await api('/admin/users' + (id ? '/' + id : ''), id ? 'PUT' : 'POST', {
                ...form,
                managerId: form.managerId || null,
              });
              await load();
            });
          }}
        >
          <div className="form-grid">
            {[
              ['username', t('Utilizator', 'Username')],
              ['name', t('Nume complet', 'Full name')],
              ['email', 'Email'],
              ['worker', t('Cod Worker', 'Worker code')],
            ].map(([k, n]) => (
              <Field name={n} key={k}>
                <input
                  required
                  value={form[k] || ''}
                  type={k === 'email' ? 'email' : 'text'}
                  disabled={k === 'username' && !!id}
                  onChange={(e) => set(k, e.target.value)}
                />
              </Field>
            ))}
            <Field name={t('Centru de cost implicit al angajatului', 'Employee default cost centre')}
              note={t('Se precompletează în antet și în fiecare bon nou; fiecare câmp poate fi modificat independent.',
                      'Prefilled in the report header and each new receipt; both can be changed independently.')}>
              <select value={form.costCenter || ''} onChange={(e) => set('costCenter', e.target.value)}>
                <option value="">{t('Fără centru atribuit', 'No cost centre assigned')}</option>
                {costCentres.map((m: Any) => (
                  <option key={m.id} value={m.code}>{m.code} · {t(m.nameRo, m.nameEn)}</option>
                ))}
              </select>
            </Field>
            <Field name={t('Manager organizațional (nu stabilește aprobatorul)', 'Organisational manager (not the approver)')}
              note={t('Aprobarea este rutată exclusiv după centrele de cost ale bonurilor.',
                      'Approval is routed exclusively from receipt cost centre ownership.')}>
              <select
                value={form.managerId || ''}
                onChange={(e) => set('managerId', e.target.value)}
              >
                <option value="">{t('Fără manager', 'No manager')}</option>
                {users
                  .filter((u) => u.active && u.roles.includes('Manager') && u.id !== id)
                  .map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name}
                    </option>
                  ))}
              </select>
            </Field>
            <Field name={t('Regula de aprobare', 'Approval rule')}
              note={t('Implicit, aprobatorul este managerul centrului cu suma cea mai mare. Doar excepțiile nominalizate folosesc un manager fix.',
                      'Normally approval follows the highest-total cost centre. A named fixed manager is an explicit exception.')}>
              <select value={form.approvalMode || 'CostCenter'}
                onChange={(e) => F({ ...form, approvalMode: e.target.value, fixedApproverId: '' })}>
                <option value="CostCenter">{t('În funcție de centrul de cost', 'By cost centre')}</option>
                <option value="Fixed">{t('Manager fix (excepție)', 'Named manager (exception)')}</option>
              </select>
            </Field>
            {form.approvalMode === 'Fixed' && (
              <Field name={t('Aprobator fix', 'Fixed approver')}
                note={t('Managerul trebuie să fie activ și diferit de angajat.',
                        'The manager must be active and must not be the employee.')}>
                <select required value={form.fixedApproverId || ''}
                  onChange={(e) => set('fixedApproverId', e.target.value)}>
                  <option value="">{t('Alege managerul responsabil', 'Choose responsible manager')}</option>
                  {users.filter((u) => u.active && u.roles.includes('Manager') && u.id !== id)
                    .map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
                </select>
              </Field>
            )}
            {!id && (
              <Field name={t('Parolă temporară', 'Temporary password')}>
                <input type="password" minLength={12} autoComplete="new-password"
                  required value={form.password}
                  onChange={(e) => set('password', e.target.value)} />
              </Field>
            )}
            <Field name={t('Motiv', 'Reason')}>
              <input
                value={form.reason || ''}
                required={!!id}
                onChange={(e) => set('reason', e.target.value)}
              />
            </Field>
            <Field name={t('Acces', 'Access')}>
              <select
                value={String(form.active)}
                onChange={(e) => set('active', e.target.value === 'true')}
              >
                <option value="true">{t('Activ', 'Active')}</option>
                <option value="false">{t('Dezactivat', 'Disabled')}</option>
              </select>
            </Field>
          </div>
          <div className="role-picker">
            {['Employee', 'Manager', 'Finance', 'Admin', 'Auditor', 'FinanceReopen'].map((role) => (
              <label key={role}>
                <input
                  type="checkbox"
                  checked={form.roles.includes(role)}
                  onChange={(e) =>
                    set(
                      'roles',
                      e.target.checked
                        ? [...form.roles, role]
                        : form.roles.filter((r: string) => r !== role),
                    )
                  }
                />
                {role}
              </label>
            ))}
          </div>
          <div className="actions">
            <button className="primary">{t('Salvează contul', 'Save account')}</button>
            {id && (
              <>
                <button
                  type="button"
                  className="secondary"
                  disabled={form.reason.length < 3}
                  onClick={() =>
                    run(async () => {
                      await api('/admin/users/' + id + '/unlock', 'POST', { reason: form.reason });
                      await load();
                    })
                  }
                >
                  {t('Deblochează contul', 'Unlock account')}
                </button>
                <button
                  type="button"
                  className="text-button"
                  onClick={() => {
                    I('');
                    F({
                      username: '',
                      name: '',
                      email: '',
                      worker: '',
                      costCenter: '',
                      managerId: '',
                      approvalMode: 'CostCenter',
                      fixedApproverId: '',
                      active: true,
                      roles: ['Employee'],
                      password: '',
                      reason: '',
                    });
                  }}
                >
                  {t('Cont nou', 'New account')}
                </button>
              </>
            )}
          </div>
        </form>
        {id && (
          <section className="admin-password-reset" aria-label={t('Resetare parolă separată', 'Separate password reset')}>
            <h3>{t('Resetare parolă – acțiune separată', 'Password reset – separate action')}</h3>
            <p className="small muted">
              {t('Resetarea afectează doar utilizatorul selectat și nu modifică celelalte date ale contului.',
                'Reset affects only the selected user and does not modify other account fields.')}
            </p>
            <form onSubmit={(event) => {
              event.preventDefault();
              if (resetPassword.length < 12 || resetReason.trim().length < 3) return;
              if (!window.confirm(t('Resetezi parola pentru ', 'Reset password for ') +
                (users.find((user) => user.id === id)?.name || form.username) + '?')) return;
              run(async () => {
                await api('/admin/users/' + id + '/reset', 'POST', {
                  password: resetPassword,
                  reason: resetReason.trim(),
                });
                ResetPassword('');
                ResetReason('');
                await load();
              });
            }}>
              <div className="form-grid">
                <Field name={t('Parolă temporară nouă', 'New temporary password')}>
                  <input type="password" autoComplete="new-password" required minLength={12}
                    value={resetPassword} onChange={(event) => ResetPassword(event.target.value)} />
                </Field>
                <Field name={t('Motivul resetării', 'Reset reason')}>
                  <input required minLength={3} value={resetReason}
                    onChange={(event) => ResetReason(event.target.value)} />
                </Field>
              </div>
              <button type="submit" className="secondary"
                disabled={resetPassword.length < 12 || resetReason.trim().length < 3}>
                {t('Confirmă resetarea parolei', 'Confirm password reset')}
              </button>
            </form>
          </section>
        )}
        <section className="admin-user-search" aria-label={t('Căutare utilizatori', 'User search')}>
          <h3>{t('Găsește un utilizator', 'Find a user')}</h3>
          <div className="form-grid">
            <Field name={t('Caută nume, email, cod sau centru', 'Search name, email, code or centre')}>
              <input type="search" value={userSearch}
                onChange={(event) => UserSearch(event.target.value)} />
            </Field>
            <Field name={t('Rol', 'Role')}>
              <select value={userRole} onChange={(event) => UserRole(event.target.value)}>
                <option value="">{t('Toate rolurile', 'All roles')}</option>
                {['Employee', 'Manager', 'Finance', 'Admin', 'Auditor', 'FinanceReopen'].map((role) => (
                  <option value={role} key={role}>{role}</option>
                ))}
              </select>
            </Field>
            <Field name={t('Activitate cont', 'Account activity')}>
              <select value={userActivity} onChange={(event) => UserActivity(event.target.value)}>
                <option value="all">{t('Toate conturile', 'All accounts')}</option>
                <option value="active">{t('Active', 'Active')}</option>
                <option value="inactive">{t('Dezactivate', 'Disabled')}</option>
              </select>
            </Field>
          </div>
          <div className="actions">
            <p role="status" className="small muted">
              {visibleUsers.length} {t('din', 'of')} {users.length} {t('utilizatori', 'users')}
            </p>
            <button type="button" className="secondary" onClick={() => {
              UserSearch(''); UserRole(''); UserActivity('all');
            }}>
              {t('Resetează filtrele', 'Reset filters')}
            </button>
          </div>
        </section>
        <div className="queue">
          {visibleUsers.map((u) => (
            <button
              className="simple-row row-button"
              key={u.id}
              onClick={() => {
                I(u.id);
                F({ ...u, username: u.userName, password: '', reason: '' });
                ResetPassword('');
                ResetReason('');
              }}
            >
              <span>
                <strong>
                  {u.name} {u.active ? '' : '· ' + t('Dezactivat', 'Disabled')}
                </strong>
                <small>
                  {u.userName} · {u.email} · {t('Centru de cost', 'Cost centre')}: {u.costCenter || '—'} · {u.approvalMode === 'Fixed'
                    ? t('Aprobator fix', 'Fixed approver') + ': ' + (users.find((m) => m.id === u.fixedApproverId)?.name || '—')
                    : t('Aprobare după centru', 'Cost-centre approval')}
                </small>
              </span>
              <span>{u.roles.join(' · ')}</span>
              <ArrowUpRight size={16} />
            </button>
          ))}
        </div>
      </section>
    </>
  );
}
export function CardsPage({ t, masters, label, run, open }: Any) {
  const [data, D] = useState<Any>({ transactions: [], matches: [] }),
    [suggestions, S] = useState<Any[]>([]),
    [card, C] = useState(''),
    [csv, Csv] = useState(''),
    [preview, P] = useState<Any | null>(null),
    [tx, Tx] = useState(''),
    [line, L] = useState(''),
    [amount, A] = useState('');
  const load = async () => {
    D(await api('/finance/transactions'));
    S(await api('/finance/suggestions'));
  };
  useEffect(() => {
    run(load);
  }, []);
  const bank = { cardId: card, csv, columns: {} };
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">{t('RECONCILIERE', 'RECONCILIATION')}</span>
          <h1>{t('Carduri și tranzacții', 'Cards & transactions')}</h1>
          <p>
            {t(
              'Importă extrasul, verifică sugestiile și confirmă alocările.',
              'Import a statement, review suggestions and confirm allocations.',
            )}
          </p>
        </div>
      </div>
      <section className="panel">
        <h2>{t('Import extras CSV', 'Import CSV statement')}</h2>
        <a href="/api/finance/transactions/template" className="text-button">
          {t('Descarcă exemplu extras CSV', 'Download example bank statement CSV')}
        </a>
        <Field name={t('Card', 'Card')}>
          <select value={card} onChange={(e) => C(e.target.value)}>
            <option value="">{t('Selectează', 'Select')}</option>
            {masters
              .filter((m: Any) => m.kind === 'cards' && m.active)
              .map((m: Any) => (
                <option key={m.id} value={m.id}>
                  {label(m)}
                </option>
              ))}
          </select>
        </Field>
        <label className="upload-button">
          <Upload size={16} />
          {t('Selectează extrasul CSV', 'Select CSV statement')}
          <input
            type="file"
            accept=".csv"
            onChange={async (e) => {
              const file = e.target.files?.[0];
              if (file) Csv(await file.text());
            }}
          />
        </label>
        <p className="muted small">
          {t(
            'Coloane: reference, date, merchant, amount, currency, settledRon. Dată YYYY-MM-DD; separator virgulă; punct zecimal.',
            'Columns: reference, date, merchant, amount, currency, settledRon. Date YYYY-MM-DD; comma separator; decimal point.',
          )}
        </p>
        <textarea
          className="csv-preview"
          value={csv}
          onChange={(e) => Csv(e.target.value)}
          placeholder="reference,date,merchant,amount,currency,settledRon"
        />
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
            className="primary"
            disabled={!preview || preview.duplicates.length > 0}
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
          <p>
            {preview.count} {t('tranzacții', 'transactions')} · {preview.duplicates.length}{' '}
            {t('duplicate', 'duplicates')}
          </p>
        )}
      </section>
      <section className="panel queue">
        <h2>{t('Sugestii de asociere', 'Match suggestions')}</h2>
        {suggestions
          .filter((s) => compare(s.remaining, '0') !== 0)
          .map((s) => (
            <div className="transaction" key={s.transaction.id}>
              <div className="simple-row">
                <span>
                  <strong>{s.transaction.merchant}</strong>
                  <small>
                    {s.transaction.reference} · {s.transaction.date}
                  </small>
                </span>
                <strong>
                  {money(s.remaining)} {s.transaction.currency}
                </strong>
              </div>
              {s.candidates.map((candidate: Any) => (
                <div className="suggestion" key={candidate.id}>
                  <span>
                    {t('Potrivire', 'Match')} {candidate.score}/100 · {money(candidate.amount)}
                  </span>
                  <button
                    className="text-button"
                    onClick={() => {
                      Tx(s.transaction.id);
                      L(candidate.id);
                      A(minimum(s.remaining, candidate.amount));
                    }}
                  >
                    {t('Selectează', 'Select')}
                  </button>
                  <button className="text-button" onClick={() => open(candidate.reportId)}>
                    {t('Deschide decontul', 'Open report')}
                  </button>
                </div>
              ))}
            </div>
          ))}
        <div className="form-grid">
          <Field name={t('Tranzacție', 'Transaction')}>
            <select value={tx} onChange={(e) => Tx(e.target.value)}>
              <option value="">{t('Selectează', 'Select')}</option>
              {data.transactions.map((x: Any) => (
                <option key={x.id} value={x.id}>
                  {x.reference} · {x.merchant}
                </option>
              ))}
            </select>
          </Field>
          <Field name={t('ID cheltuială', 'Expense ID')}>
            <input value={line} onChange={(e) => L(e.target.value)} />
          </Field>
          <Field
            name={t('Sumă alocată în moneda tranzacției', 'Allocated transaction currency amount')}
          >
            <input type="number" step="0.01" value={amount} onChange={(e) => A(e.target.value)} />
          </Field>
        </div>
        <button
          className="primary"
          disabled={!tx || !line || !amount}
          onClick={() =>
            run(async () => {
              const s = suggestions.find((s) => s.transaction.id === tx);
              const candidate = s?.candidates.find((l: Any) => l.id === line);
              if (!candidate)
                throw new Error(
                  t('Selectează o cheltuială sugerată.', 'Select a suggested expense.'),
                );
              const detail = await api('/reports/' + candidate.reportId);
              await api('/finance/matches', 'POST', {
                reportId: candidate.reportId,
                version: detail.report.version,
                lineId: line,
                transactionId: tx,
                amount: String(amount),
              });
              await load();
            })
          }
        >
          {t('Confirmă alocarea', 'Confirm allocation')}
        </button>
      </section>
    </>
  );
}
export function DelegationPage({ t, run }: Any) {
  const [rows, R] = useState<Any[]>([]),
    [users, U] = useState<Any[]>([]),
    [form, F] = useState({ managerId: '', delegateId: '', from: today(), to: today() });
  const load = () => api('/admin/delegations').then(R);
  useEffect(() => {
    run(async () => {
      await load();
      U(await api('/admin/users'));
    });
  }, []);
  return (
    <section className="panel">
      <h1>{t('Delegări de aprobare', 'Approval delegations')}</h1>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          run(async () => {
            await api('/admin/delegations', 'POST', form);
            await load();
          });
        }}
      >
        <div className="form-grid">
          {(['managerId', 'delegateId'] as const).map((k) => (
            <Field key={k} name={k === 'managerId' ? 'Manager' : t('Delegat', 'Delegate')}>
              <select
                required
                value={form[k]}
                onChange={(e) => F({ ...form, [k]: e.target.value })}
              >
                <option value="">{t('Selectează', 'Select')}</option>
                {users
                  .filter((u) => u.active && u.roles.includes('Manager'))
                  .map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name}
                    </option>
                  ))}
              </select>
            </Field>
          ))}
          {(['from', 'to'] as const).map((k) => (
            <Field key={k} name={k === 'from' ? t('Din', 'From') : t('Până la', 'To')}>
              <input
                type="date"
                required
                value={form[k]}
                onChange={(e) => F({ ...form, [k]: e.target.value })}
              />
            </Field>
          ))}
        </div>
        <button className="primary">{t('Creează delegare', 'Create delegation')}</button>
      </form>
      {rows.map((d) => (
        <div className="simple-row" key={d.id}>
          <span>
            {users.find((u) => u.id === d.managerId)?.name} →{' '}
            {users.find((u) => u.id === d.delegateId)?.name}
            <small>
              {d.from} — {d.to}
            </small>
          </span>
          <button
            className="secondary danger"
            onClick={() =>
              run(async () => {
                await api('/admin/delegations/' + d.id, 'DELETE');
                await load();
              })
            }
          >
            {t('Revocă', 'Revoke')}
          </button>
        </div>
      ))}
    </section>
  );
}
export function DataPage({ path, title, t, run }: Any) {
  const [rows, R] = useState<Any[]>([]);
  useEffect(() => {
    run(async () => R(await api(path)));
  }, [path]);
  return (
    <section className="panel">
      <div className="section-heading">
        <h1>{title}</h1>
        <a href="/api/analytics/csv" className="secondary">
          <Download size={16} />
          {t('Raport CSV', 'CSV report')}
        </a>
      </div>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>{t('Moment', 'Time')}</th>
              <th>{t('Utilizator', 'Actor')}</th>
              <th>{t('Acțiune', 'Action')}</th>
              <th>{t('Entitate', 'Entity')}</th>
              <th>{t('Motiv', 'Reason')}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id}>
                <td>{new Date(r.at).toLocaleString()}</td>
                <td>{r.actor}</td>
                <td>{r.action}</td>
                <td>{r.entity}</td>
                <td>{r.reason}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
