import { VehicleSettings, PostingPreview, PostingDashboard } from './PostingPanels';
import { TaxPolicyPanel, FinanceAcceptancePanel } from './FinanceControls';
import { CurrencySettings } from './FinancialWorkspace';
import { ResetPanel } from './FinanceControls';
import { add, sum, formatMoney, minimum, compare, masterJson, formatOriginal } from './decimal.mjs';
import FinancialWorkspace from './FinancialWorkspace';
import ProfilePage from './ProfilePage';
import EmployeeImport from './EmployeeImport';
import Reconciliation from './Reconciliation';
import PasswordRecovery from './PasswordRecovery';
import {
  Comments,
  ConfigurationImport,
  Assignments,
  FinanceWorklist,
  Reporting,
  RevisionComparison,
  AuditSearch,
  FinanceDashboard,
  FinanceRouting,
} from './PlatformPages';
import CapturePanel from './CapturePanel';
import ExpenseModal, { newReceipt } from './ReceiptModal';
import ReportAttachments from './ReportAttachments';
import { Field } from './Fields';
import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import {
  LayoutDashboard,
  Receipt,
  CheckCheck,
  WalletCards,
  Settings,
  Users,
  ShieldCheck,
  Bell,
  ArrowUpRight,
  Plus,
  LogOut,
  Search,
  ChevronLeft,
  FileText,
  Upload,
  Download,
  Globe,
  Building2,
  Clock,
  Menu,
  X,
  CheckCircle2,
  AlertCircle,
} from 'lucide-react';
import { api } from './api';
import './style.css';
import { money, stateText } from './ReportDisplay';
import { Stat, ReportList } from './ReportList';
import { ReportHelp, OcrHelp } from './QuickGuides';
import { JobsPage, ReportJobs } from './BackgroundJobs';
import { FinanceQueue, BatchPanel } from './FinanceQueues';
import { ManagerNextActions } from './ManagerFinanceActions';
import { MasterPage, UsersPage, CardsPage, DelegationPage, DataPage } from './AdminPages';
type Any = Record<string, any>;
type Master = Any;
const today = () => new Date().toISOString().slice(0, 10);
const eventLabels: Record<string, [string, string]> = {
  OcrReviewReady: [
    'Documentele sunt pregătite pentru verificare',
    'Documents are ready for review',
  ],
  OcrProcessingFailed: [
    'Extragerea documentelor necesită intervenție',
    'Document extraction needs attention',
  ],
  HeaderChanged: ['Date generale modificate', 'Report header changed'],
  DocumentVerified: ['Document verificat', 'Document verified'],
  HumanCorrectionApplied: ['Valori OCR corectate', 'OCR values corrected'],
  DocumentGroupsChanged: ['Gruparea documentelor modificată', 'Document grouping changed'],
  PageDispositionChanged: ['Pagina sursă clasificată', 'Source page classified'],
  DocumentReviewStarted: ['Verificarea paginilor începută', 'Page review started'],
  VerifiedSourceReopened: ['Document redeschis pentru verificare', 'Source reopened for review'],
  ReportDocumentArchived: ['Document arhivat', 'Document archived'],
  Submitted: ['Trimis la aprobare', 'Submitted for approval'],
  approve: ['Aprobat', 'Approved'],
  reject: ['Respins', 'Rejected'],
  Returned: ['Returnat pentru corecții', 'Returned for correction'],
  VendorRequired: ['Furnizor de înregistrat', 'Supplier registration required'],
  VendorResolved: ['Furnizor înregistrat', 'Supplier registered'],
  ERPReady: ['Gata pentru ERP', 'Ready for ERP'],
  ReapprovalRequired: ['Reaprobare necesară', 'Reapproval required'],
  Reminder: ['Reminder de procesare', 'Processing reminder'],
  Escalation: ['Escalare de procesare', 'Processing escalation'],
  CriticalJobFailure: ['Operațiune eșuată', 'Failed operation'],
  Created: ['Decont creat', 'Report created'],
  PurposeChanged: ['Scop modificat', 'Purpose changed'],
  LineAdded: ['Bon adăugat', 'Receipt added'],
  LineChanged: ['Bon modificat', 'Receipt changed'],
  LineArchived: ['Bon arhivat', 'Receipt archived'],
  LineDuplicated: ['Bon duplicat', 'Receipt duplicated'],
  OcrSuggestionsReviewed: ['Propuneri OCR verificate', 'OCR suggestions reviewed'],
  ExtractionRequested: ['Extragere solicitată', 'Extraction requested'],
  ERPExported: ['Export ERP generat', 'ERP export generated'],
  BatchDownloaded: ['Export descărcat', 'Export downloaded'],
  PostedBatchReopened: ['Decont redeschis după reversare', 'Report reopened after reversal'],
  ReportRoutingCorrected: ['Angajat sau manager corectat', 'Employee or manager corrected'],
  CommentAdded: ['Comentariu adăugat', 'Comment added'],
  complete: ['Verificare Finance finalizată', 'Finance review completed'],
  close: ['Decont închis', 'Report closed'],
};
const eventTitle = (code: string, t: any) =>
  t(...(eventLabels[code.split(':')[0]] || [code, code]));
function App() {
  const [lang, SetLang] = useState(localStorage.getItem('decont.language') === 'en' ? 'en' : 'ro');
  const L = (value: string) => {
    localStorage.setItem('decont.language', value);
    SetLang(value);
  };
  const t = (ro: string, en: string) => (lang === 'ro' ? ro : en);
  const [me, M] = useState<Any | null>(null),
    [loading, Loading] = useState(true),
    [page, P] = useState('dashboard'),
    [reports, R] = useState<Any[]>([]),
    [masters, Masters] = useState<Master[]>([]),
    [detail, D] = useState<Any | null>(null),
    [error, E] = useState(''),
    [notice, N] = useState(''),
    [busy, B] = useState(false),
    [mobile, Mobile] = useState(false),
    [notices, Notices] = useState<Any[]>([]),
    [showNotices, SN] = useState(false);
  const role = (r: string) => me?.roles.includes(r);
  const label = (m: Any) => (lang === 'ro' ? m.nameRo : m.nameEn || m.nameRo);
  async function run(work: () => Promise<any>) {
    B(true);
    E('');
    try {
      return await work();
    } catch (e) {
      E((e as Error).message);
    } finally {
      B(false);
    }
  }
  async function load() {
    const [rs, ms, ns] = await Promise.all([api('/reports'), api('/masters'), api('/notices')]);
    R(rs);
    Masters(ms);
    Notices(ns);
  }
  useEffect(() => {
    api('/me')
      .then(async (u) => {
        M(u);
        if (!u.changePassword) await load();
      })
      .catch(() => {})
      .finally(() => Loading(false));
  }, []);
  useEffect(() => {
    document.documentElement.lang = lang;
    localStorage.setItem('decont.language', lang);
  }, [lang]);
  async function open(id: string) {
    await run(async () => {
      D(await api('/reports/' + id));
      P('detail');
    });
  }
  async function refresh(id?: string) {
    await load();
    if (id) D(await api('/reports/' + id));
  }
  async function change(path: string, body: any, method = 'POST') {
    return run(async () => {
      const result = await api(path, method, body);
      await refresh(detail?.report.id);
      N(t('Modificările au fost salvate.', 'Changes saved.'));
      return result;
    });
  }
  const [username, U] = useState(''),
    [password, Password] = useState(''),
    [newPassword, NP] = useState('');
  if (loading)
    return (
      <div className="splash">
        <div className="brand-symbol">d.</div>
        <p>{t('Se încarcă spațiul de lucru…', 'Loading workspace…')}</p>
      </div>
    );
  if (!me)
    return (
      <div className="internal-login">
        <section className="internal-login-panel panel">
          <div className="section-heading">
            <h1>{t('Deconturi de cheltuieli', 'Expense reports')}</h1>
            <button className="language" onClick={() => L(lang === 'ro' ? 'en' : 'ro')}>
              <Globe size={17} />
              {lang === 'ro' ? 'English' : 'Română'}
            </button>
          </div>
          <h2>{t('Autentificare', 'Sign in')}</h2>
          <p className="muted">
            {t(
              'Aplicație internă. Folosește contul primit de la administrator.',
              'Internal application. Use the account supplied by your administrator.',
            )}
          </p>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              run(async () => {
                await api('/auth/login', 'POST', { username, password });
                const u = await api('/me');
                M(u);
                if (!u.changePassword) await load();
              });
            }}
          >
            <Field name={t('Utilizator', 'Username')}>
              <input
                autoComplete="username"
                required
                value={username}
                onChange={(e) => U(e.target.value)}
              />
            </Field>
            <Field name={t('Parolă', 'Password')}>
              <input
                autoComplete="current-password"
                required
                type="password"
                value={password}
                onChange={(e) => Password(e.target.value)}
              />
            </Field>
            {error && (
              <div className="error" role="alert">
                {error}
              </div>
            )}
            <button disabled={busy} className="primary wide">
              {t('Autentificare', 'Sign in')}
            </button>
          </form>
          <PasswordRecovery t={t} />
          <p className="small muted">
            {t(
              'Pentru acces sau resetarea parolei, contactează administratorul.',
              'Contact your administrator for access or password resets.',
            )}
          </p>
        </section>
      </div>
    );
  if (me.changePassword)
    return (
      <div className="splash">
        <button className="language" onClick={() => L(lang === 'ro' ? 'en' : 'ro')}>
          <Globe size={16} />
          {lang === 'ro' ? 'English' : 'Română'}
        </button>
        <form
          className="panel password"
          onSubmit={(e) => {
            e.preventDefault();
            run(async () => {
              await api('/auth/change-password', 'POST', { current: password, new: newPassword });
              M(await api('/me'));
              await load();
            });
          }}
        >
          <ShieldCheck size={36} />
          <h2>{t('Alege o parolă nouă', 'Choose a new password')}</h2>
          <p>
            {t(
              'Minimum 12 caractere, cu majuscule, minuscule, cifre și simboluri.',
              'At least 12 characters, with uppercase, lowercase, numbers and symbols.',
            )}
          </p>
          <Field name={t('Parola temporară', 'Temporary password')}>
            <input
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => Password(e.target.value)}
              required
            />
          </Field>
          <Field name={t('Parola nouă', 'New password')}>
            <input
              type="password"
              autoComplete="new-password"
              minLength={12}
              value={newPassword}
              onChange={(e) => NP(e.target.value)}
              required
            />
          </Field>
          {error && <div className="error">{error}</div>}
          <button className="primary" disabled={busy}>
            {t('Salvează și continuă', 'Save and continue')}
          </button>
        </form>
      </div>
    );
  const nav = [
    ['dashboard', LayoutDashboard, t('Privire de ansamblu', 'Overview')],
    ['reports', Receipt, t('Deconturi', 'Expense reports')],
    ['reporting', FileText, t('Raportare', 'Reporting')],
    ['profile', Users, t('Profilul meu', 'My profile')],
    ...(role('Manager') ? [['approvals', CheckCheck, t('Aprobări', 'Approvals')]] : []),
    ...(role('Finance')
      ? [
          ['finance', Building2, 'Finance'],
          ['posting', FileText, t('Situație contabilă', 'Accounting overview')],
          ['cards', WalletCards, t('Carduri și tranzacții', 'Cards & transactions')],
          ['finance-settings', Settings, t('Configurare Finance', 'Finance settings')],
        ]
      : []),
    ...(role('Finance') || role('Admin')
      ? [['settings', Settings, t('Nomenclatoare', 'Master data')]]
      : []),
    ...(role('Admin')
      ? [
          ['users', Users, t('Utilizatori și acces', 'Users & access')],
          ['delegations', Clock, t('Delegări', 'Delegations')],
          ['assignments', Users, t('Alocări organizaționale', 'Organization assignments')],
          ['jobs', Clock, t('Operațiuni de fundal', 'Background jobs')],
        ]
      : []),
    ...(role('Finance') || role('Admin') || role('Auditor')
      ? [['audit', ShieldCheck, t('Jurnal de audit', 'Audit log')]]
      : []),
  ];
  const state = (s: string) => stateText[s]?.[lang === 'ro' ? 0 : 1] || s;
  const list = (rows: Any[]) => (
    <ReportList rows={rows} t={t} state={state} open={open} lang={lang} />
  );
  const unfinishedReport = role('Employee')
    ? reports.find(
        (r) =>
          r.employeeId === me.id &&
          ['DRAFT', 'REJECTED', 'EMPLOYEE_CORRECTION'].includes(r.state),
      )
    : null;
  return (
    <div className="shell">
      <aside className={mobile ? 'sidebar open' : 'sidebar'}>
        <div className="brand">
          <span className="brand-symbol">d.</span> decont
          <button
            className="mobile-only icon"
            aria-label={t('Închide meniul', 'Close menu')}
            onClick={() => Mobile(false)}
          >
            <X />
          </button>
        </div>
        <span className="nav-caption">{t('SPAȚIU DE LUCRU', 'WORKSPACE')}</span>
        <nav>
          {nav.map(([id, Icon, text]: any) => (
            <button
              key={id}
              className={page === id || (page === 'detail' && id === 'reports') ? 'active' : ''}
              aria-current={page === id || (page === 'detail' && id === 'reports') ? 'page' : undefined}
              onClick={() => {
                P(id);
                Mobile(false);
              }}
            >
              <Icon size={19} />
              {text}
            </button>
          ))}
        </nav>
        <div className="sidebar-note">
          {t(
            'Aplicație internă · Acces conform rolurilor',
            'Internal application · Role-based access',
          )}
        </div>
        <div className="user">
          <span className="avatar">{me.name?.slice(0, 2).toUpperCase()}</span>
          <div>
            <strong>{me.name}</strong>
            <small>{me.roles.join(' · ')}</small>
          </div>
          <button
            className="icon"
            aria-label={t('Deconectare', 'Sign out')}
            onClick={() =>
              run(async () => {
                await api('/auth/logout', 'POST');
                M(null);
                D(null);
                Password('');
              })
            }
          >
            <LogOut size={18} />
          </button>
        </div>
      </aside>
      <div className="workspace">
        <header>
          <div className="breadcrumb">
            <button
              className="icon mobile-only"
              aria-label={t('Deschide meniul', 'Open menu')}
              onClick={() => Mobile(true)}
            >
              <Menu />
            </button>
            <span>{t('Spațiu de lucru', 'Workspace')}</span>
            <span>/</span>
            <strong>
              {page === 'detail'
                ? detail?.report.number
                : (nav.find((n) => n[0] === page)?.[2] as string)}
            </strong>
          </div>
          <div className="header-actions">
            <button className="language" onClick={() => L(lang === 'ro' ? 'en' : 'ro')}>
              <Globe size={16} />
              {lang === 'ro' ? 'RO' : 'EN'}
            </button>
            <button
              className="icon bell"
              aria-label={t('Notificări', 'Notifications')}
              onClick={() => SN(!showNotices)}
            >
              <Bell size={20} />
              {notices.some((n) => !n.read) && <i />}
            </button>
            <button
              className="avatar small-avatar"
              aria-label={t('Profilul meu', 'My profile')}
              onClick={() => P('profile')}
            >
              {me.name?.slice(0, 2).toUpperCase()}
            </button>
          </div>
        </header>
        {showNotices && (
          <div className="notifications panel">
            <h3>{t('Notificări', 'Notifications')}</h3>
            {!notices.length && <p>{t('Nu ai notificări noi.', 'No notifications yet.')}</p>}
            {notices.map((n) => (
              <button
                key={n.id}
                onClick={() =>
                  run(async () => {
                    await api('/notices/' + n.id + '/read', 'POST');
                    if (n.reportId !== '00000000-0000-0000-0000-000000000000')
                      await open(n.reportId);
                    else P('jobs');
                    SN(false);
                    await load();
                  })
                }
              >
                <span className={n.read ? '' : 'unread'}>{eventTitle(n.event, t)}</span>
                <small>{new Date(n.at).toLocaleString(lang)}</small>
              </button>
            ))}
          </div>
        )}
        <main>
          {error && (
            <div className="error" role="alert">
              <AlertCircle size={18} />
              <span>{error}</span>
              <button onClick={() => E('')} className="icon">
                <X size={17} />
              </button>
            </div>
          )}
          {notice && (
            <div className="success" role="status">
              <CheckCircle2 size={18} />
              {notice}
              <button className="icon" onClick={() => N('')}>
                <X size={16} />
              </button>
            </div>
          )}
          {page === 'posting' && role('Finance') && (
            <PostingDashboard t={t} run={run} open={open} />
          )}
          {page === 'finance-settings' && role('Finance') && (
            <>
              <div className="page-heading">
                <div>
                  <h1>{t('Configurare Finance', 'Finance settings')}</h1>
                  <p>
                    {t(
                      'Monede, rotunjire, politici TVA și acceptanță. Configurarea este disponibilă și fără un decont deschis.',
                      'Currencies, rounding, VAT policies and acceptance. Configuration is available without opening a report.',
                    )}
                  </p>
                </div>
              </div>
              <section className="panel">
                <CurrencySettings t={t} run={run} refresh={load} />
                <VehicleSettings t={t} run={run} refresh={load} />
                <TaxPolicyPanel t={t} run={run} me={me} masters={masters} />
                <FinanceAcceptancePanel t={t} run={run} masters={masters} />
              </section>
            </>
          )}
          {page === 'dashboard' && (
            <>
              <div className="page-heading">
                <div>
                  <span className="eyebrow">{t('DECONTURI', 'EXPENSE REPORTS')}</span>
                  <h1>{t('Privire de ansamblu', 'Overview')}</h1>
                  <p>
                    {t(
                      'Deconturi, aprobări și situația documentelor.',
                      'Reports, approvals and document status.',
                    )}
                  </p>
                </div>
                {role('Employee') && (
                  <button
                    className="primary"
                    disabled={busy}
                    onClick={() =>
                      run(async () => {
                        const r = await api('/reports', 'POST', { purpose: '' });
                        await refresh();
                        await open(r.id);
                      })
                    }
                  >
                    <Plus size={18} />
                    {t('Decont nou', 'New report')}
                  </button>
                )}
              </div>
              {unfinishedReport && (
                <section className="panel dashboard-next-step">
                  <div>
                    <h2>{t('Ai un decont de completat', 'You have a report to finish')}</h2>
                    <p className="muted">
                      {t(
                        'Continuă de unde ai rămas. Datele existente sunt păstrate.',
                        'Pick up where you left off. Your existing details are kept.',
                      )}
                    </p>
                  </div>
                  <button className="secondary" onClick={() => open(unfinishedReport.id)}>
                    {t('Continuă decontul', 'Continue report')}
                    <ArrowUpRight size={17} />
                  </button>
                </section>
              )}
              <div className="stats">
                <Stat
                  title={t('Total deconturi', 'Total reports')}
                  value={String(reports.length)}
                  icon={<Receipt />}
                  note={t('În spațiul tău de lucru', 'In your workspace')}
                />
                <Stat
                  title={t('În curs de aprobare', 'Awaiting approval')}
                  value={String(
                    reports.filter((r) =>
                      role('Manager')
                        ? r.canApprove
                        : ['MANAGER_REVIEW', 'REAPPROVAL_REQUIRED'].includes(r.state),
                    ).length,
                  )}
                  icon={<Clock />}
                  note={t('Următorul pas: manager', 'Next step: manager')}
                />
                <Stat
                  title={t('Valoare totală', 'Total amount')}
                  value={money(sum(reports.map((r) => r.total))) + ' RON'}
                  icon={<WalletCards />}
                  note={t('Valori estimate sau reconciliate', 'Estimated or reconciled values')}
                />
              </div>
              <div className="stats">
                {role('Employee') && (
                  <Stat
                    title={t('Rambursare personală în curs', 'Personal reimbursement in progress')}
                    value={
                      money(
                        sum(
                          reports
                            .filter(
                              (r) =>
                                r.employeeId === me.id &&
                                !['DRAFT', 'POSTED', 'CLOSED'].includes(r.state),
                            )
                            .map((r) => r.personal),
                        ),
                      ) + ' RON'
                    }
                    icon={<WalletCards />}
                    note={t('Deconturile tale trimise', 'Your submitted reports')}
                  />
                )}{' '}
                {role('Manager') && (
                  <Stat
                    title={t('Cea mai veche aprobare', 'Oldest pending approval')}
                    value={
                      String(
                        Math.max(0, ...reports.filter((r) => r.canApprove).map((r) => r.ageDays)),
                      ) + t(' zile', ' days')
                    }
                    icon={<Clock />}
                    note={t('Aprobări alocate sau delegate', 'Assigned or delegated approvals')}
                  />
                )}
              </div>
              {role('Manager') && (
                <ManagerNextActions reports={reports} open={open} t={t} />
              )}
              {role('Finance') && (
                <FinanceDashboard t={t} run={run} openQueue={() => P('finance')} />
              )}
              <div className="dashboard-grid">
                <section className="panel">
                  <div className="section-heading">
                    <h2>{t('Deconturi recente', 'Recent reports')}</h2>
                    <button className="text-button" onClick={() => P('reports')}>
                      {t('Vezi toate', 'View all')}
                      <ArrowUpRight size={16} />
                    </button>
                  </div>
                  {list(reports.slice(0, 5))}
                </section>
                <section className="panel report-instructions">
                  <h2>{t('Ai nevoie de ajutor?', 'Need a hand?')}</h2>
                  <p>
                    {t(
                      'Poți folosi fotografii, PDF-uri sau completarea manuală. Verifică originalele înainte de trimitere.',
                      'Use photos, PDFs or manual entry. Check the originals before submitting.',
                    )}
                  </p>
                  <ReportHelp t={t} />
                  <OcrHelp t={t} />
                </section>
              </div>
            </>
          )}
          {['reports', 'approvals', 'finance'].includes(page) && (
            <>
              <div className="page-heading">
                <div>
                  <span className="eyebrow">{t('LISTĂ DECONTURI', 'REPORT LIST')}</span>
                  <h1>
                    {page === 'approvals'
                      ? t('Aprobări', 'Approvals')
                      : page === 'finance'
                        ? t('Verificare financiară', 'Finance review')
                        : t('Deconturi', 'Expense reports')}
                  </h1>
                  <p>
                    {page === 'approvals'
                      ? t('Verifică bonurile și justificările înainte să aprobi sau să respingi.',
                          'Review receipts and explanations before approving or rejecting.')
                      : page === 'finance'
                        ? t('Identifică excepțiile, verifică documentele și continuă spre ERP.',
                            'Find exceptions, review evidence and prepare reports for ERP.')
                        : t('Deschide un decont pentru documente și istoricul deciziilor.',
                            'Open a report to review documents and decision history.')}
                  </p>
                </div>
                {role('Employee') && (
                  <button
                    className="primary"
                    onClick={() =>
                      run(async () => {
                        const r = await api('/reports', 'POST', { purpose: '' });
                        await load();
                        await open(r.id);
                      })
                    }
                  >
                    <Plus size={18} />
                    {t('Decont nou', 'New report')}
                  </button>
                )}
              </div>
              <section className="panel">
                {list(
                  reports.filter((r) =>
                    page === 'approvals'
                      ? r.canApprove
                      : page === 'finance'
                        ? r.state !== 'DRAFT'
                        : true,
                  ),
                )}
              </section>
              {page === 'finance' && (
                <>
                  <FinanceWorklist t={t} run={run} open={open} masters={masters} label={label} />
                  <FinanceQueue t={t} run={run} />
                </>
              )}
            </>
          )}
          {page === 'detail' && detail && (
            <ReportDetail
              key={detail.report.id}
              detail={detail}
              me={me}
              masters={masters}
              label={label}
              t={t}
              state={state}
              busy={busy}
              run={run}
              change={change}
              refresh={() => refresh(detail.report.id)}
              back={() => P('reports')}
            />
          )}
          {page === 'profile' && <ProfilePage me={me as any} lang={lang} setLanguage={L} />}{' '}
          {page === 'reporting' && <Reporting t={t} run={run} open={open} />}{' '}
          {page === 'assignments' && <Assignments t={t} run={run} />}{' '}
          {page === 'settings' && <MasterPage t={t} run={run} refresh={load} />}
          {page === 'users' && <UsersPage t={t} run={run} />}
          {page === 'cards' && (
            <Reconciliation t={t} masters={masters} label={label} run={run} open={open} />
          )}
          {page === 'delegations' && <DelegationPage t={t} run={run} />}
          {page === 'audit' && <AuditSearch t={t} run={run} />}{' '}
          {page === 'jobs' && <JobsPage t={t} run={run} />}
          <footer>
            decont <span>·</span>{' '}
            {t('Aplicație internă de deconturi', 'Internal expense application')}
          </footer>
        </main>
      </div>
    </div>
  );
}
function receiptWithSource(editing: Any, r: Any, detail: Any) {
  const group = detail.sourceGroups?.find((g: Any) => g.expenseId === editing.id);
  const attachment = r.attachments?.find(
    (a: Any) =>
      !a.archived &&
      (group
        ? a.id === group.bundleId
        : editing.id && JSON.parse(a.coveredLines || '[]').includes(editing.id)),
  );
  const regions = group ? JSON.parse(group.regions) : [];
  const sourceOriginal =
    editing.sourceOriginal ||
    (attachment
      ? {
          ...attachment,
          reportId: r.id,
          url:
            '/api/report-attachments/' +
            attachment.id +
            (attachment.mime === 'application/pdf' ? '#page=' + (regions[0]?.page || 1) : ''),
          previewUrl:
            attachment.mime === 'image/heic'
              ? '/api/reports/' + r.id + '/documents/' + attachment.id + '/preview'
              : null,
        }
      : null);
  return {
    ...editing,
    reportId: r.id,
    sourceOriginal,
    sourceAttachmentId: editing.sourceAttachmentId || group?.bundleId,
    sourceGroupId: editing.sourceGroupId || group?.id,
    ocrCandidate:
      editing.ocrCandidate ||
      (group
        ? {
            id: group.id,
            fields: JSON.parse(group.fields),
            taxes: JSON.parse(group.taxes),
            regions,
            pages: Array.from(new Set(regions.map((v: Any) => v.page))),
          }
        : undefined),
  };
}
function ReportDetail({
  detail,
  me,
  masters,
  label,
  t,
  state,
  busy,
  change,
  refresh,
  run,
  back,
}: Any) {
  const r = detail.report;
  const [purpose, Purpose] = useState(r.purpose),
    [tab, T] = useState('expenses'),
    [editing, Editing] = useState<Any | null>(null),
    [reason, Reason] = useState(''),
    [selectedDoc, SD] = useState<Any | null>(null);
  const [headerPayment, HP] = useState(r.payment || r.lines[0]?.data.payment || 'Personal'),
    [headerCard, HC] = useState(r.cardId || r.lines[0]?.data.cardId || '');
  useEffect(() => {
    Purpose(r.purpose);
    HP(r.payment || r.lines[0]?.data.payment || 'Personal');
    HC(r.cardId || r.lines[0]?.data.cardId || '');
  }, [r.id, r.purpose, r.payment, r.cardId]);
  const own =
    me.roles.includes('Employee') &&
    r.employeeId === me.id &&
    ['DRAFT', 'REJECTED', 'EMPLOYEE_CORRECTION'].includes(r.state);
  const finance = me.roles.includes('Finance'),
    editable =
      own ||
      (finance &&
        ['FINANCE_REVIEW', 'READY_FOR_ERP', 'PENDING_VENDOR', 'PENDING_CARD_MATCH'].includes(
          r.state,
        ));
  const decision =
    me.roles.includes('Manager') &&
    ['MANAGER_REVIEW', 'REAPPROVAL_REQUIRED'].includes(r.state) &&
    r.employeeId !== me.id;
  const version = { version: r.version, reason };
  const action = (a: string) => change('/reports/' + r.id + '/actions/' + a, version);
  const blockingCount = (detail.issues || []).filter((issue: Any) => issue.blocking).length;
  const nextStep = own
    ? !r.payment
      ? t('Alege mai întâi metoda de plată.', 'Choose how you paid first.')
      : !r.lines.length
        ? t(
            'Adaugă primul bon: fotografie, PDF sau manual.',
            'Add your first receipt: photo, PDF or manual entry.',
          )
        : blockingCount
          ? t(
              `Corectează ${blockingCount} ${blockingCount === 1 ? 'problemă' : 'probleme'} înainte de trimitere.`,
              `Fix ${blockingCount} ${blockingCount === 1 ? 'issue' : 'issues'} before submitting.`,
            )
          : t(
              'Verifică bonurile și trimite decontul la aprobare.',
              'Review your receipts and submit for approval.',
            )
    : decision
      ? t('Verifică bonurile și apoi aprobă sau respinge.', 'Review the receipts, then approve or reject.')
      : finance && ['FINANCE_REVIEW', 'READY_FOR_ERP'].includes(r.state)
        ? t(
            'Verifică documentele, sumele și excepțiile înainte de finalizare.',
            'Review documents, amounts and exceptions before finishing.',
          )
        : null;
  return (
    <>
      <button className="text-button back" onClick={back}>
        <ChevronLeft size={17} />
        {t('Înapoi la deconturi', 'Back to reports')}
      </button>
      <div className="page-heading">
        <div>
          <span className="eyebrow">
            {r.number} · {t('REVIZIA', 'REVISION')} {r.revision}
          </span>
          <h1>{r.purpose || t('Decont nou', 'New expense report')}</h1>
          <span className={'badge ' + r.state.toLowerCase()}>{state(r.state)}</span>
        </div>
        <div className="actions">
          <a
            className="secondary"
            href={'/api/reports/' + r.id + '/pdf'}
            target="_blank"
            rel="noreferrer"
          >
            <FileText size={16} />
            {t('Descarcă PDF', 'Download PDF')}
          </a>
          <a className="secondary" href={'/api/reports/' + r.id + '/pack'}>
            <Download size={16} />
            {t('Dosar decont', 'Report pack')}
          </a>
          {finance && (
            <a className="secondary" href={'/api/reports/' + r.id + '/accounting-details'}>
              <Download size={16} />
              {t('Bonuri și TVA (XLSX)', 'Receipts and VAT (XLSX)')}
            </a>
          )}
        </div>
      </div>
      {nextStep && (
        <section className="report-next-step" aria-label={t('Următorul pas', 'Next step')}>
          <strong>{t('Următorul pas', 'Next step')}</strong>
          <p>{nextStep}</p>
        </section>
      )}
      <div className="report-summary">
        <div>
          <small>{t('Total decont', 'Report total')}</small>
          <strong>
            {money(r.total)} <span>RON</span>
          </strong>
        </div>
        <div>
          <small>{t('Rambursare personală', 'Personal reimbursement')}</small>
          <strong>
            {money(r.personal)} <span>RON</span>
          </strong>
        </div>
        <div>
          <small>{t('Cheltuieli / Documente', 'Expenses / Documents')}</small>
          <strong>
            {r.lines.length}{' '}
            <span>
              /{' '}
              {r.lines.reduce((s: number, l: Any) => s + l.evidence.length, 0) +
                (r.attachments?.length || 0)}
            </span>
          </strong>
        </div>
      </div>
      <div className="expense-tags">
        {Array.from(new Set<string>(r.lines.map((l: Any) => l.data.currency))).map((currency) => (
          <span key={currency}>
            {t('Total documente', 'Document total')} {currency}:{' '}
            {money(
              sum(
                r.lines
                  .filter((l: Any) => l.data.currency === currency)
                  .map((l: Any) => l.data.amount),
              ),
            )}
          </span>
        ))}
        <span>
          {t('Estimare totală', 'Total estimate')}:{' '}
          {money(sum(r.lines.map((l: Any) => l.estimatedRon)))} RON
        </span>
        <span>
          {t('Decontat la bancă (bonuri reconciliate)', 'Bank-settled (reconciled receipts)')}:{' '}
          {money(sum(r.lines.filter((l: Any) => l.reconciled).map((l: Any) => l.settledRon)))} RON
        </span>
      </div>
      <section className="panel compact">
        <h2>{t('Date generale', 'Report header')}</h2>
        <div className="form-grid">
          <Field name={t('Nume angajat', 'Employee name')}>
            <input
              readOnly
              value={
                r.employeeName ||
                t('Nedisponibil în istoricul acestui decont', 'Not recorded for this report')
              }
            />
          </Field>
          <Field name="LantID">
            <input readOnly value={r.employeeWorker || t('Neconfigurat', 'Not configured')} />
          </Field>
          <Field name={t('Departament', 'Department')}>
            <input
              readOnly
              value={
                r.departmentCode ||
                t(
                  'Neconfigurat — contactează administratorul',
                  'Not configured — contact your administrator',
                )
              }
            />
          </Field>
        </div>
        <p className="small muted">
          {t(
            'Date preluate din profil la crearea decontului. Bonurile se adaugă separat mai jos.',
            'Profile details captured when the report was created. Add individual receipts below.',
          )}
        </p>
      </section>
      {editable && (
        <section className="panel compact">
          <div className="form-grid">
            <Field name={t('Metoda de plată a decontului', 'Report payment method')}>
              <select
                disabled={r.lines.length > 0}
                value={headerPayment}
                onChange={(e) => {
                  HP(e.target.value);
                  HC('');
                }}
              >
                <option value="Personal">{t('Plată personală', 'Personal payment')}</option>
                <option value="CompanyCard">{t('Card companie', 'Company card')}</option>
              </select>
            </Field>
            {headerPayment === 'CompanyCard' && (
              <Field name={t('Cardul decontului', 'Report company card')}>
                <select
                  disabled={r.lines.length > 0}
                  value={headerCard}
                  onChange={(e) => HC(e.target.value)}
                >
                  <option value="">{t('Selectează cardul', 'Select card')}</option>
                  {masters
                    .filter(
                      (m: Any) =>
                        m.kind === 'cards' &&
                        m.active &&
                        m.from <= today() &&
                        (!m.to || m.to >= today()),
                    )
                    .map((m: Any) => (
                      <option key={m.id} value={m.id}>
                        {label(m)}
                      </option>
                    ))}
                </select>
              </Field>
            )}
          </div>
          <p className="small muted">
            {t(
              'Alege o singură dată. Toate bonurile vor folosi aceeași metodă și același card. Pentru alte date de plată, creează un decont separat.',
              'Choose once. All receipts use this payment method and card. Create a separate report for different payment details.',
            )}
          </p>
          <div className="header-edit">
            <Field name={t('Scopul decontului', 'Business purpose')}>
              <input
                value={purpose}
                onChange={(e) => Purpose(e.target.value)}
                placeholder={t('De exemplu, deplasare la client', 'For example, client visit')}
              />
            </Field>
            {!own && (
              <Field name={t('Motivul modificării', 'Reason for change')}>
                <input value={reason} onChange={(e) => Reason(e.target.value)} required />
              </Field>
            )}
            <button
              className="secondary"
              disabled={
                busy ||
                (headerPayment === 'CompanyCard' && !headerCard) ||
                (purpose === r.purpose &&
                  headerPayment === r.payment &&
                  (headerCard || null) === (r.cardId || null))
              }
              onClick={() =>
                change(
                  '/reports/' + r.id,
                  {
                    ...version,
                    purpose,
                    payment: headerPayment,
                    cardId: headerPayment === 'CompanyCard' ? headerCard : null,
                  },
                  'PATCH',
                )
              }
            >
              {t('Salvează datele generale', 'Save report header')}
            </button>
          </div>
        </section>
      )}
      {finance && editable && (
        <FinanceRouting report={r} t={t} run={run} refresh={refresh} reason={reason} />
      )}
      <div className="tabs">
        {[
          ['expenses', t('Cheltuieli', 'Expenses')],
          ['financials', t('Calcul și FX', 'Calculations & FX')],
          ['history', t('Istoric și aprobări', 'History & approvals')],
          ...(finance || me.roles.includes('Auditor') ? [['erp', 'ERP']] : []),
        ].map(([id, name]) => (
          <button key={id} className={tab === id ? 'active' : ''} onClick={() => T(id)}>
            {name}
          </button>
        ))}
      </div>
      {tab === 'expenses' && editable && r.payment && (
        <CapturePanel
          report={r}
          t={t}
          run={run}
          refresh={refresh}
          reason={reason}
          edit={Editing}
          masters={masters}
        />
      )}
      {tab === 'expenses' && (
        <div className="detail-grid">
          <section className="panel">
            <div className="section-heading">
              <h2>
                {t('Cheltuieli', 'Expenses')} <span className="count">{r.lines.length}/50</span>
              </h2>
              {editable && (
                <button
                  className="secondary"
                  disabled={r.lines.length >= 50 || !r.payment}
                  onClick={() => Editing(newReceipt())}
                >
                  <Plus size={16} />
                  {t('Adaugă bon / document', 'Add receipt / document')}
                </button>
              )}
            </div>
            {!r.lines.length && (
              <div className="empty">
                <Receipt size={36} />
                <h3>{t('Începe cu prima cheltuială.', 'Start with your first expense.')}</h3>
                <p>
                  {t(
                    'Completează datele și atașează documentul original.',
                    'Enter the details and attach the original document.',
                  )}
                </p>
              </div>
            )}
            {r.lines.map((l: Any) => (
              <article className="expense" key={l.id}>
                <div className="expense-head">
                  <span className="expense-icon">
                    <Receipt size={21} />
                  </span>
                  <div>
                    <h3>
                      {l.position}. {l.data.supplier || l.data.description}
                    </h3>
                    <p>
                      {l.data.invoice ? l.data.invoice + ' · ' : ''}
                      {l.data.date} ·{' '}
                      {l.data.payment === 'Personal'
                        ? t('Plată personală', 'Personal payment')
                        : t('Card companie', 'Company card')}
                    </p>
                  </div>
                  <strong>
                    {money(l.financeRon ?? (l.reconciled ? l.settledRon : l.estimatedRon))}{' '}
                    <small>RON</small>
                  </strong>
                </div>
                <p>{l.data.description}</p>
                <ReceiptAccounting line={l} masters={masters} t={t} />
                <div className="expense-tags">
                  <span>
                    {masters.find(
                      (m: Any) => m.kind === 'expenseTypes' && m.code === l.data.category,
                    )
                      ? label(
                          masters.find(
                            (m: Any) => m.kind === 'expenseTypes' && m.code === l.data.category,
                          ),
                        )
                      : l.data.category}
                  </span>
                  <span>{l.data.costCenter}</span>
                  {l.reconciled && (
                    <span className="matched">{t('Reconciliat', 'Reconciled')}</span>
                  )}
                </div>
                <div className="evidence">
                  {l.evidence.map((e: Any) => (
                    <div key={e.id}>
                      <FileText size={16} />
                      {e.scan === 'Clean' ? (
                        <button className="text-button" onClick={() => SD(e)}>
                          {e.name}
                        </button>
                      ) : (
                        <span>{e.name}</span>
                      )}
                      <span className="small muted">
                        {e.scan === 'Clean'
                          ? t('Verificat', 'Scanned')
                          : e.scan === 'Rejected'
                            ? t('Respins la scanare', 'Scan rejected')
                            : t('În verificare', 'Scan pending')}
                      </span>
                      {e.ocr && (
                        <details>
                          <summary>OCR · {t('Verificare manuală', 'Manual verification')}</summary>
                          <pre className="ocr-text">{JSON.parse(e.ocr).text}</pre>
                        </details>
                      )}
                      {editable && (
                        <button
                          className="icon"
                          aria-label={t('Arhivează', 'Archive')}
                          onClick={() =>
                            change(`/reports/${r.id}/evidence/${e.id}/archive`, version)
                          }
                        >
                          <X size={14} />
                        </button>
                      )}
                    </div>
                  ))}
                </div>
                <div className="expense-actions">
                  {editable && (
                    <>
                      <button className="text-button" onClick={() => Editing(l)}>
                        {t('Editează', 'Edit')}
                      </button>
                      <button
                        className="text-button"
                        onClick={() => change(`/reports/${r.id}/lines/${l.id}/duplicate`, version)}
                      >
                        {t('Duplică', 'Duplicate')}
                      </button>
                      <button
                        className="text-button danger"
                        onClick={() => change(`/reports/${r.id}/lines/${l.id}/archive`, version)}
                      >
                        {t('Arhivează', 'Archive')}
                      </button>
                      <div className="receipt-upload">
                        <Field
                          name={t(
                            'Fișiere pentru bonul ' + l.position,
                            'Files for receipt ' + l.position,
                          )}
                        >
                          <input
                            type="file"
                            multiple
                            accept=".pdf,.jpg,.jpeg,.png,.heic,.heif"
                            onChange={(e) => {
                              const files = Array.from(e.target.files || []);
                              e.target.value = '';
                              if (files.length)
                                run(async () => {
                                  let currentVersion = r.version;
                                  try {
                                    for (const file of files) {
                                      const form = new FormData();
                                      form.append('file', file);
                                      form.append('version', String(currentVersion));
                                      form.append('reason', reason);
                                      const saved = await api(
                                        `/reports/${r.id}/lines/${l.id}/evidence`,
                                        'POST',
                                        form,
                                      );
                                      currentVersion = saved.version;
                                    }
                                  } finally {
                                    await refresh();
                                  }
                                });
                            }}
                          />
                        </Field>
                        <small>
                          {t(
                            'Selectează fișierul bonului sau folosește fișierul comun de la final.',
                            'Browse receipt files or use the combined file at the end.',
                          )}
                        </small>
                      </div>
                    </>
                  )}
                  {finance && editable && (
                    <>
                      <button
                        className="text-button"
                        disabled={reason.trim().length < 3}
                        onClick={() =>
                          change(`/finance/reports/${r.id}/lines/${l.id}/disposition`, {
                            ...version,
                            kind: 'notDuplicate',
                          })
                        }
                      >
                        {t('Nu este duplicat', 'Not a duplicate')}
                      </button>
                      <button
                        className="text-button danger"
                        disabled={reason.trim().length < 3}
                        onClick={() =>
                          change(`/finance/reports/${r.id}/lines/${l.id}/disposition`, {
                            ...version,
                            kind: 'duplicate',
                          })
                        }
                      >
                        {t('Confirmă duplicat și arhivează', 'Confirm duplicate and archive')}
                      </button>
                      <button
                        className="text-button"
                        onClick={() =>
                          change(`/finance/reports/${r.id}/lines/${l.id}/disposition`, {
                            ...version,
                            kind: 'duplicateAllowed',
                          })
                        }
                      >
                        {t('Justifică duplicatul', 'Resolve duplicate')}
                      </button>
                      {l.data.payment === 'CompanyCard' && (
                        <button
                          className="text-button"
                          onClick={() =>
                            change(`/finance/reports/${r.id}/lines/${l.id}/disposition`, {
                              ...version,
                              kind: 'cardOverride',
                            })
                          }
                        >
                          {t('Excepție reconciliere', 'Reconciliation exception')}
                        </button>
                      )}
                    </>
                  )}
                </div>
              </article>
            ))}
          </section>
          <aside className="review-panel panel">
            {selectedDoc && (
              <div className="inline-document">
                <div className="section-heading">
                  <strong>{selectedDoc.name}</strong>
                  <button className="icon" onClick={() => SD(null)}>
                    <X size={16} />
                  </button>
                </div>
                {selectedDoc.mime === 'application/pdf' ? (
                  <iframe title={selectedDoc.name} src={'/api/evidence/' + selectedDoc.id} />
                ) : selectedDoc.mime === 'image/heic' ? (
                  <img
                    alt={selectedDoc.name}
                    src={`/api/reports/${r.id}/documents/${selectedDoc.id}/preview`}
                  />
                ) : (
                  <img src={'/api/evidence/' + selectedDoc.id} alt={selectedDoc.name} />
                )}
              </div>
            )}
            <h2>{t('Verificare înainte de trimitere', 'Submission checklist')}</h2>
            <p className="muted">
              {t(
                'Aceleași verificări se aplică și la salvarea pe server.',
                'The same checks are enforced on the server.',
              )}
            </p>
            {detail.issues.length ? (
              detail.issues.map((p: Any, i: number) => (
                <div className={'issue ' + (p.blocking ? 'blocking' : 'warning')} key={i}>
                  <AlertCircle size={17} />
                  <span>
                    {p.lineId && (
                      <strong>
                        {t('Bon', 'Receipt')}{' '}
                        {r.lines.find((l: Any) => l.id === p.lineId)?.position}:{' '}
                      </strong>
                    )}
                    {t(p.ro, p.en)}
                    {p.lineId && editable && (
                      <button
                        className="text-button"
                        onClick={() => Editing(r.lines.find((l: Any) => l.id === p.lineId))}
                      >
                        {t('Corectează bonul', 'Correct receipt')}
                      </button>
                    )}
                  </span>
                </div>
              ))
            ) : (
              <div className="issue ready">
                <CheckCircle2 size={18} />
                {t('Datele sunt complete.', 'Details are complete.')}
              </div>
            )}
            <button className="secondary wide" onClick={() => run(refresh)}>
              {t('Actualizează verificările', 'Refresh checks')}
            </button>
            {own && (
              <button
                className="primary wide"
                disabled={busy || detail.issues.some((p: Any) => p.blocking)}
                onClick={() => change('/reports/' + r.id + '/submit', version)}
              >
                {t('Trimite la aprobare', 'Submit for approval')}
                <ArrowUpRight size={17} />
              </button>
            )}
            <div className="privacy">
              <ShieldCheck size={17} />
              {t(
                'Documentele originale sunt păstrate cu istoricul de modificări.',
                'Original documents are retained with change history.',
              )}
            </div>
          </aside>
        </div>
      )}
      <Comments detail={detail} t={t} run={run} refresh={refresh} allowed={decision || finance} />
      {tab === 'expenses' && (
        <ReportAttachments
          report={r}
          editable={editable}
          busy={busy}
          reason={reason}
          t={t}
          run={run}
          refresh={refresh}
        />
      )}
      {tab === 'history' && <RevisionComparison detail={detail} t={t} run={run} />}{' '}
      {tab === 'history' && <ReportJobs report={r} t={t} run={run} />}{' '}
      {tab === 'history' && (
        <section className="panel">
          <h2>{t('Istoric decont', 'Report history')}</h2>
          <div className="timeline">
            {detail.timeline.map((a: Any) => (
              <div key={a.id}>
                <span className="timeline-dot" />
                <strong>{eventTitle(a.action, t)}</strong>
                <small>
                  {new Date(a.at).toLocaleString()} · {a.actor}
                </small>
                {a.reason && <p>{a.reason}</p>}
              </div>
            ))}
          </div>
          <h3>{t('Revizii trimise', 'Submitted revisions')}</h3>
          {detail.snapshots.map((s: Any) => (
            <p key={s.revision}>
              <a
                href={`/api/reports/${r.id}/snapshots/${s.revision}`}
                target="_blank"
                rel="noreferrer"
              >
                {t('Revizia', 'Revision')} {s.revision}
              </a>{' '}
              · {new Date(s.at).toLocaleString()}
            </p>
          ))}
        </section>
      )}
      {tab === 'financials' && (
        <FinancialWorkspace
          report={r}
          t={t}
          run={run}
          refresh={refresh}
          finance={finance}
          editable={editable}
          me={me}
          masters={masters}
        />
      )}
      {tab === 'history' && (
        <ResetPanel report={r} t={t} run={run} refresh={refresh} editable={editable} />
      )}
      {tab === 'erp' && (
        <section className="panel">
          <PostingPreview
            report={r}
            detail={detail}
            t={t}
            run={run}
            finance={finance}
            refresh={refresh}
            busy={busy}
            change={change}
          />
          {finance && <BatchPanel report={r} me={me} t={t} run={run} refresh={refresh} />}
        </section>
      )}
      {(decision || finance) && (
        <section className="panel decision-panel">
          <Field name={t('Motiv / observații', 'Reason / comments')}>
            <textarea
              value={reason}
              onChange={(e) => Reason(e.target.value)}
              placeholder={t(
                'Obligatoriu pentru respingere, returnare și modificări Finance.',
                'Required for rejection, return and Finance changes.',
              )}
            />
          </Field>
          <div className="actions">
            {decision && (
              <>
                <button className="primary" disabled={busy} onClick={() => action('approve')}>
                  <CheckCheck size={18} />
                  {t('Aprobă revizia', 'Approve revision')}
                </button>
                <button
                  className="secondary danger"
                  disabled={busy || reason.trim().length < 3}
                  onClick={() => action('reject')}
                >
                  {t('Respinge', 'Reject')}
                </button>
              </>
            )}
            {finance && ['FINANCE_REVIEW', 'READY_FOR_ERP'].includes(r.state) && (
              <>
                <button className="primary" disabled={busy} onClick={() => action('complete')}>
                  {t('Finalizează verificarea', 'Complete finance review')}
                </button>
                <button
                  className="secondary"
                  disabled={busy || reason.trim().length < 3}
                  onClick={() => action('return')}
                >
                  {t('Returnează angajatului', 'Return to employee')}
                </button>
              </>
            )}
            {finance && r.state === 'POSTED' && (
              <button className="primary" onClick={() => action('close')}>
                {t('Închide decontul', 'Close report')}
              </button>
            )}
            {finance && (
              <button
                className="secondary"
                disabled={reason.trim().length < 3}
                onClick={() =>
                  change('/finance/reports/' + r.id + '/legal-hold', {
                    ...version,
                    hold: !r.legalHold,
                  })
                }
              >
                {r.legalHold
                  ? t('Ridică blocarea legală', 'Release legal hold')
                  : t('Aplică blocare legală', 'Apply legal hold')}
              </button>
            )}
          </div>
        </section>
      )}
      {editing && (
        <ExpenseModal
          key={editing.id || editing.draftKey}
          expense={receiptWithSource(editing, r, detail)}
          reportPayment={r.payment || r.lines.find((l: Any) => l.id !== editing.id)?.data.payment}
          reportCardId={
            r.cardId || r.lines.find((l: Any) => l.id !== editing.id)?.data.cardId || null
          }
          finance={finance}
          draftStorageKey={
            editing.sourceGroupId ? 'ocr-draft:' + editing.sourceGroupId : 'receipt-draft:' + r.id
          }
          masters={masters}
          label={label}
          t={t}
          busy={busy}
          onClose={() => Editing(null)}
          saveDraft={
            editing.sourceGroupId && !editing.id
              ? async (data: Any) => {
                  await api(
                    `/reports/${r.id}/documents/${editing.sourceAttachmentId}/groups/${editing.sourceGroupId}/fields`,
                    'PATCH',
                    { ...version, data },
                  );
                  await api('/preferences/ocr-draft:' + editing.sourceGroupId, 'PUT', {}).catch(
                    () => {},
                  );
                  await refresh();
                  Editing(null);
                }
              : undefined
          }
          save={async (data: Any, files: File[]) => {
            const existingIds = new Set(r.lines.map((l: Any) => l.id));
            const result = await change(
              '/reports/' + r.id + '/lines' + (editing.id ? '/' + editing.id : ''),
              {
                ...version,
                data,
                sourceAttachmentId: editing.sourceAttachmentId || null,
                sourceGroupId: editing.sourceGroupId || null,
              },
              editing.id ? 'PATCH' : 'POST',
            );
            if (!result) return;
            if (!editing.id)
              await api(
                '/preferences/' +
                  (editing.sourceGroupId
                    ? 'ocr-draft:' + editing.sourceGroupId
                    : 'receipt-draft:' + r.id),
                'PUT',
                {},
              ).catch(() => {});
            const savedLine = editing.id
              ? result.lines.find((l: Any) => l.id === editing.id)
              : result.lines.find((l: Any) => !existingIds.has(l.id));
            if (!files.length) {
              Editing(null);
              return;
            }
            Editing(savedLine);
            await run(async () => {
              let currentVersion = result.version;
              try {
                for (const file of files) {
                  const form = new FormData();
                  form.append('file', file);
                  form.append('version', String(currentVersion));
                  form.append('reason', reason);
                  const uploaded = await api(
                    `/reports/${r.id}/lines/${savedLine.id}/evidence`,
                    'POST',
                    form,
                  );
                  currentVersion = uploaded.version;
                }
                Editing(null);
              } finally {
                await refresh();
              }
            });
          }}
        />
      )}
    </>
  );
}
function ReceiptAccounting({ line, masters, t }: Any) {
  const d = line.data;
  const vat = d.country === 'RO' && (d.hasCompanyCui ?? d.documentType === 'InvoiceWithCui');
  return (
    <div className="receipt-accounting">
      <div>
        <span>
          {d.country === 'RO'
            ? t('Furnizor din România', 'Romanian supplier')
            : t('Furnizor din străinătate', 'Foreign supplier')}
        </span>
        {d.country === 'RO' && (
          <span>
            {vat
              ? t('Cu CUI-ul companiei', 'Company VAT ID printed')
              : t('Fără CUI-ul companiei', 'Company VAT ID not printed')}
          </span>
        )}
        {vat && (
          <span>
            {t('CUI furnizor', 'Supplier VAT ID')}: {d.cui}
          </span>
        )}
        <span>
          {t('Total bon', 'Receipt total')}: {formatOriginal(d.amount)} {d.currency}
        </span>
      </div>
      {vat && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>{t('Cotă TVA', 'VAT rate')}</th>
                <th>{t('Bază', 'Base')}</th>
                <th>{t('TVA plătit', 'VAT paid')}</th>
                <th>{t('Total', 'Total')}</th>
              </tr>
            </thead>
            <tbody>
              {Array.from(
                new Set<number>([
                  0,
                  11,
                  21,
                  ...d.taxes.map((tax: Any) =>
                    Number(
                      JSON.parse(masters.find((m: Any) => m.id === tax.rateId)?.json || '{}')
                        .rate || 0,
                    ),
                  ),
                ]),
              ).map((pct) => {
                const row = d.taxes.find((tax: Any) => {
                  const master = masters.find((m: Any) => m.id === tax.rateId);
                  return master && Number(JSON.parse(master.json).rate) === pct;
                });
                return (
                  <tr key={pct}>
                    <td>{pct}%</td>
                    <td>{formatOriginal(row?.base || 0)}</td>
                    <td>{formatOriginal(row?.vat || 0)}</td>
                    <td>
                      {formatOriginal(add(row?.base || '0', row?.vat || '0'))}{' '}
                      {d.taxCurrency || 'RON'}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
createRoot(document.getElementById('root')!).render(<App />);
