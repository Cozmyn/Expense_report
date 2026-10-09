# Faza 2: proiectare / Phase 2: redesign plan

2026-10-09 · Status: implementation-ready design baseline, not shipped behavior.

## 1. Experți și decizii / Council decisions

Design, UX Research, Front Office, Back Office and Bilingual Content lead this phase. Architecture, OCR, QA, Compliance/Data and Product Ownership review the contracts and acceptance gates. Preserve every source ID; implementation status remains unchanged. Romanian is the default UI; English is complete and selectable. This is an internal operational application, without promotional banners or slogans.

Keep the existing React/TypeScript + ASP.NET Core + PostgreSQL architecture. Refactor by feature and contract rather than rewriting the platform. Keep simple cookie authentication and background workers. Preserve the exact ERP Import format and the user-approved section 5.1/5.2 dispositions. Financial data is never omitted to simplify a screen.

## 2. Sistem vizual / Design system

| Token | Value | Usage |
|---|---|---|
| canvas / surface / subtle | #F5F7FA / #FFFFFF / #EEF2F6 | Page, cards, grouped content |
| text / muted | #172B3A / #526171 | Primary and helper text; avoid pale green text |
| primary / hover | #075E50 / #064C42 | Primary actions and selected navigation |
| border / control-border | #D7E0E7 / #7A8997 | Dividers versus interactive boundaries |
| focus | #1D4ED8 | 3px outline, 3px offset, never suppressed |
| success | #166534 on #F0FDF4 | Saved/verified with icon and text |
| warning | #854D0E on #FEFCE8 | Review needed, not color-only |
| error | #B91C1C on #FEF2F2 | Inline errors and failed operations |
| font | system sans, Romanian glyph support | 16px body, 14px table/helper, 12px metadata only |
| type scale | 12 / 14 / 16 / 20 / 24 / 32px | 1.5 body line height; tabular numerals for money |
| spacing | 4 / 8 / 12 / 16 / 24 / 32 / 48px | Reusable spacing tokens |
| radius | 6px controls, 12px cards, 16px dialogs | Consistent surfaces |
| elevation | subtle border first; shadow only for overlays | Calm visual hierarchy |
| target | 44×44px default interaction area | No dense icon-only touch targets |

Light theme is the delivery baseline. Dark mode remains optional, not silently promised. Keep readable documents on a white document canvas. Verify actual rendered combinations, disabled states, focus and charts for WCAG 2.2 AA; token selection alone is not certification.

Desktop: 240px role-scoped navigation, 64px header, 24–32px content gutters. Wide workbenches may use full width; reading summaries capped at 960px. At <1024px collapse side navigation; at <768px use stacked task panels and receipt steps. At 320px width/400% zoom, avoid page-wide horizontal scrolling; only genuinely two-dimensional tables may scroll within a labelled region.

## 3. Componente și stări / Components and states

Build shared AppShell, PageHeader, Breadcrumbs, LanguageSwitch, Button, IconButton, FormField, MoneyInput, DateInput, CuiInput, SearchCombobox, StatusBadge, Alert, EmptyState, Skeleton, Stepper, DataTable, FilterBar, ConfirmDialog, Timeline, DocumentViewer, UploadZone and SaveIndicator. Build domain ReceiptEditor, VatBreakdown, EvidenceCoverage, OcrFieldReview, PreflightChecklist, AllocationEditor and RevisionDiff on these primitives.

Each input owns a stable id, visible label, helper/error association and required/optional text. Dynamic N/A fields disappear with a short explanation when the branch changes; existing values remain in historical revisions but cannot leak into active posting. Error summary links focus the exact field. Server field rules and validation are authoritative.

| State | UI behavior | Recovery |
|---|---|---|
| First use | Explain the task in one sentence; show one primary action | Optional dismissible tour; training data only in isolated training mode |
| Loading | Stable skeleton for the affected region; announce busy state | Do not replace the whole page or erase input |
| Empty result | Distinguish no records from no filter matches | Create record or clear filters, according to permission |
| Saving | «Se salvează…» / “Saving…” next to the task | Serialize writes; show server acknowledgement timestamp |
| Save failed | Persistent «Nesalvat» / “Not saved”; preserve in-memory edits | Retry or copy user-entered text; do not store receipt/financial PII in localStorage |
| Conflict | Show latest revision and local changes side by side | Reload or deliberately reapply; never last-write-wins silently |
| Success | Inline state plus polite announcement | Toast only for secondary feedback, not the sole record |
| Forbidden/deactivated | Explain that access changed without exposing protected data | Refresh authorized navigation or sign in again |
| Destructive action | Name object and consequence; reason where required | Undo draft archive only if safe; posted records require a new corrective revision |

Trap modal focus, set initial focus, support Escape when safe, and return focus to the trigger. Full-page receipt editing replaces the oversized modal; small confirmation dialogs remain. Respect reduced motion; no animation is required for understanding.

## 4. Navigație și pagini / Information architecture

Header always exposes RO/EN, notifications and profile. Navigation is the union of authorized role tasks, never a role selector granting permissions. Deep links restore selected record, tab and queue filters.

| Source page | RO / EN navigation | Contents and primary action |
|---|---|---|
| PUB-01 | Autentificare / Sign in | Username/password; reset and forced-change states; no role selection |
| EMP-01 | Acasă / Home | Own drafts, returned items, reimbursable totals; «Decont nou» |
| EMP-02 | Deconturile mele / My reports | Search/status list; editor with header, receipts, evidence and review |
| EMP-03 | Bon / Receipt | Full-page receipt editor; original left, fields right; 4 steps on mobile |
| EMP-04 | Detalii decont / Report details | Read-only summary, receipt drilldown, timeline, revisions, PDF/originals |
| MGR-01 | Aprobări / Approvals | Pending and History tabs; oldest actionable first; delegated scope |
| MGR-02 | Verifică decontul / Review report | Revision summary and evidence; sticky Approve / Return actions |
| FIN-01 | Financiar / Finance | Exception counts linking to exact queue filters and aging |
| FIN-02 | De verificat / Review queue | Single saved-view worklist and split accounting workbench |
| FIN-03 | Furnizori de completat / Supplier requests | Missing vendor account, impacted reports, resolve with reason |
| FIN-04 | Reconciliere carduri / Card reconciliation | Statement batches, unmatched items, proposed/confirmed allocations |
| FIN-05 | Export contabil / Accounting export | Blocking preflight, exact 24-column preview, batches/post/reopen history |
| REP-01 | Rapoarte / Reports | Spend and process views; scoped filters and exports |
| ADM-01 | Utilizatori și organizare / Users and organization | Accounts, assignments, roles, delegation, import |
| ADM-02 | Nomenclatoare / Master data | Searchable type list, effective versions, import/export, duplicate review |
| ADM-03 | Reguli și setări / Rules and settings | Field policy, VAT/FX/account mapping, reminders, operational jobs |
| AUD-01 | Jurnal de audit / Audit trail | Readable before/after, entity/user/action/date filters, complete export |
| SHR-01 | Notificări / Notifications | Persistent center with unread filter and authorized deep links |
| SHR-02 | Profilul meu / My profile | Profile, language, password change; roles read-only |

Grouping pages into shared layouts does not remove source page capabilities. Reports/Admin/Auditor visibility follows the source permission matrix, including read-only access; Finance capabilities do not automatically accompany Admin.

## 5. Angajat: completarea decontului / Employee flow

Happy path: Home → New report → purpose/header → Add receipt → four receipt steps → report review → Submit for approval → read-only status. The receipt list supports up to 50 receipts, edit, duplicate and archive. Duplication never silently duplicates original evidence or its coverage. Report-level context is entered once; smart defaults are visibly editable and never invent financial values.

Receipt steps: **Document → Date bon → Sume și TVA → Verificare** / **Document → Receipt details → Amounts and VAT → Review**. Desktop presents the same sections alongside a persistent original; users can jump to an already completed section. Mobile retains progress and an “Original” toggle; toggling never loses input.

| Condition | Fields and behavior | Accounting consequence |
|---|---|---|
| Every receipt | Category, document date, domestic/foreign branch, description, currency, gross, payment; additional fields follow authoritative policy | Stable line id, original currency and RON valuation retained |
| Foreign supplier | No mandatory Romanian CUI or domestic VAT breakdown; show description/currency/amount and applicable common fields | Foreign branch; preserve source currency and valuation policy |
| Domestic, no company CUI printed | Ask explicitly; do not confuse company CUI with supplier CUI | Simplified receipt branch; do not synthesize recoverable VAT |
| Domestic, company CUI printed | Supplier CUI input, normalized lookup; known supplier name read-only from selected version | Vendor accounting branch; supplier identity/version retained |
| Unknown valid CUI | «Verifică CUI-ul. Dacă este corect, confirmă pentru a continua.» | Save/manager approval allowed after confirmation; missing ERP vendor blocks export and creates Finance work |
| Invalid checksum | Explain correction inline | Cannot treat a malformed identifier as a confirmed supplier |
| Applicable domestic VAT | 0% base; 11/21% paid VAT and base, effective on document date | Both base and VAT persisted/exported per rate; 5% inactive for new applicable dates |
| Company card | Active card selector; owner shown as information | Worker remains expense-incurred-by employee; final RON from confirmed settlement |
| Paid personally | No card field | Effective internal FX; reimbursable totals shown separately |

VAT entry offers “TVA din bon” / “VAT on receipt” and explicit base fields. Derived base is labelled a suggestion until confirmed. At 0%, enter base only; VAT stays zero. Calculate gross from sum(base + VAT), show declared total, calculated total and difference together. Keep source decimal precision, deterministic rounding and RON-equivalent tolerance visible. ±1 RON validation and ±2 RON small-zero code-count policy remain separate; small-zero tolerance must never discard money. Vehicle 11/21% branches retain the agreed 50% deductibility.

Every conditional change (country, CUI flag, category, date, payment) refetches field policy, shows affected fields and revalidates. Do not silently carry obsolete VAT/accounting dimensions into a new branch.

Attachments: visible «Alege fișiere» / “Browse files”, camera and drop zone. Support PDF/JPG/PNG and existing HEIC conversion. Multiple files per receipt remain supported. Offer «Toate bonurile sunt într-un fișier» / “All receipts are in one file” at report review. Show explicit covered receipts and pages; new/duplicated receipts require coverage review. Submission requires each active receipt to have clean line evidence or confirmed shared coverage; pending/failed scans cannot satisfy it.

Autosave persists valid drafts after a short debounce and on step changes using version checks; invalid partial field text stays in memory with an unsaved indicator until safely representable. New incomplete-receipt draft support requires a distinct draft contract, not relaxing submit/export rules. No unsaved-success message. Submission waits for pending saves and then performs atomic server validation. Duplicate submit/retry is idempotent. Errors keep the user on review with links to exact receipts/fields.

## 6. OCR și previzualizare / OCR and preview

Original left, editable extracted fields right at desktop widths. Viewer includes document/page selector, fit-width, zoom, rotate view, page count, original download and keyboard controls. Maintain normalized coordinates [0..1] with page dimensions/rotation; overlay and focus must transform together. Native PDF embedding alone cannot fulfill field highlighting: use a maintained viewer adapter or controlled page derivatives with accessible text; select implementation after dependency/security review.

Pipeline: upload → quarantine/scan → queued extraction → page/document grouping → suggestions → human review → linked receipt. Show distinct failure states for unsupported format, encrypted PDF, size/page limit, malware, poor image quality, unavailable engine and no fields found. Manual entry remains available after safe upload when OCR fails; quarantine failure cannot be bypassed.

Extraction contract per field: field key, raw text, normalized candidate, source document/page, bounding box or explicit unavailable, provider confidence or null, score provenance, extraction version, validation findings and human review state. Provider confidence is not probability of correctness; threshold is configurable and calibrated on representative samples. Null means “confidence unavailable”, never 100%. Low-confidence and inconsistent financial fields require explicit review. Classification and multipage grouping allow user correction; a page is not automatically a separate receipt.

Selecting a field navigates to its page and highlights the region; selecting another keeps unsaved edits. If geometry is unavailable, show that fact and allow manual review. Persist suggestion→correction, reviewer, timestamp, reason where applicable and source/version. Keep originals immutable; store derivatives separately. Feedback supports evaluation and future improvement; no claim of automatic learning until a tested retraining process exists.

Pre-submit/approval summary uses the same revision and calculation projection as PDF/print/export. Preview of a historical revision never silently substitutes current master values. PDF includes all lines, totals, decisions and revision identifiers; evidence package retains originals and hashes. Exact ERP 24-column preview is a distinct Finance view, not a simplified summary.

## 7. Manager, Finance și Auditor / Operational flows

| Role/task | Happy path | Error/recovery |
|---|---|---|
| Manager | Pending queue → revision/evidence → Approve → next item; history searchable | Return requires reason, optional receipt comments; changed revision forces refresh; no self-approval; delegation effective dates enforced |
| Finance review | Exception queue → selected report/document → resolve checklist → complete review | Field change requires reason and before/after; increased reimbursement or corrected routing triggers required reapproval |
| Supplier resolution | Open request → verify identifier/master → assign ERP account → affected reports refreshed | Unknown account stays blocked; external validator outage visible; authorized reasoned override only |
| Card reconciliation | Import → mapping/preview → atomic confirm → suggestions → explicit allocation confirm | Show remaining amounts on both sides; no double allocation; support partial/one-to-many/many-to-one; reversal audited |
| ERP export | Preflight → exact rows → immutable XLSX → record posting reference/date → close | Failed generation retry idempotent; posted report cannot export again without authorized reasoned reopen/new cycle |
| Auditor | Filters → timeline/revision diff → exact evidence/export | Read-only business access; expired file link can be renewed after authorization; no silent export cap |
| Admin | Search entity → versioned edit/import preview → confirm → history | Reject entire invalid import with downloadable row errors; disable rather than erase referenced records; prevent loss of last authorized admin access |

Finance tables: server pagination default 25, options 50/100; stable sort with id tie-breaker; search, status, employee, manager, card, supplier, age, VAT/vendor/duplicate/ERP filters; named saved views and column visibility. Always distinguish selected-page items from all filtered items. Bulk operations are role/state checked per item and return individual outcomes. Bulk download/assignment may be supported; approval/posting never silently occurs through a generic bulk action. Confirmation names count, scope and irreversible consequences.

Dashboards show actionable queues and reconciled counts from the same filter contract. Keep age since entering current state distinct from report age and last edit. No invented SLA until policy is configured. Analytics includes employee, cost center, category, supplier, GL, project, card and month; show original currencies separately and explicitly labelled RON totals.

Audit timeline: actor, action, timestamp (Europe/Bucharest display; UTC storage), entity, revision, reason, before/after and privacy-minimized source/correlation. Exports use stable snapshot/cursor semantics and disclose scope/count. Audit writes append only; backup/admin privileges remain governed operationally. Restore of financial history creates a new authorized revision, never rewrites accepted evidence.

## 8. Nomenclatoare și operațiuni / Master data and operations

Maintain suppliers, expense types, countries, document types, cost centers, projects, corporate cards, VAT/tax codes, FX, GL/local/subaccounts, organizational units, field rules, ERP optional mappings and notification rules. Separate business-edit forms from raw configuration JSON. Each type exposes owner, code, bilingual names, active/effective dates and history where relevant.

Merge: select duplicate → compare fields/references → choose survivor → preview conflicts/affected active records → authorize with reason → transactional merge. Preserve aliases, old identifiers, posted snapshots and audit; never rewrite historical exports. Incompatible financial periods/codes block merge. Rollback uses an explicit compensating change if safe, not silent deletion.

CSV/XLSX import: template → upload → normalized preview with row errors → commit validated batch and file hash/mapping version → result/history. Confirm revalidates data/version even if preview was clean; invalid batches do not partially commit. Exports respect filters and permissions. Bank statement import gets durable batch/file lineage and duplicate protection.

Jobs page reports OCR, scan, email and import health with safe retry, attempts and user-facing errors. Notification center stores events even when transport fails; deduplicate reminder events independently from enabled channels. Messages link to authorized records, never carry complete receipts by email. Configure SMTP and recipients at deployment.

## 9. Arhitectură și contracte / Implementation contracts

Preserve current endpoints during incremental refactoring. Map original catalogue names to equivalent current routes explicitly; new names below are **proposed**, not existing endpoints.

| Contract/change | Implementation boundary | Required guard |
|---|---|---|
| Field policy + structured validation | Rules.cs → typed DTO; fieldPath/lineId/code/severity/RO+EN params | Same server policy consumed by editor and preflight |
| Paged queues/reporting/audit | PlatformFeatures.cs / Api.cs query DTOs | Whitelisted sort/filter, scoped SQL, stable cursors, bounded page size |
| Incomplete receipt draft | Separate draft DTO/state; existing ExpenseData remains final contract | Draft may be incomplete; submit/export never may |
| OCR review | DocumentExtraction.cs provider adapter + extraction/review records | File authorization, scan gate, source/version binding, optimistic concurrency |
| Document pages/geometry | DocumentPreviews.cs derivative service | Hash/version cache key, safe renderer limits, no original mutation |
| Financial projection | Posting/Rules/ReceiptAccountingExport/PDF shared revision context | Decimal arithmetic, effective configuration snapshots, deterministic result |
| Imports/merges | Admin/SpreadsheetImports transaction services | Dry run, revalidation, references, file lineage and audit |
| Notifications/SLA | NotificationPolicies and workflow transition events | Effective recipient routing, unique event delivery, state-entry timestamps |
| Audit/export | Append-only audit + paged complete export worker | Permission recheck, snapshot consistency, no omitted tail records |

Proposed migrations: receipt draft payload/version; OCR extraction/field/review provenance; document derivatives; statement import batches; state-transition timestamps; financial rule snapshot references; minimized audit correlation metadata; master aliases/merge records. Inspect existing schema for equivalent storage before creating any table. Migrations are additive, backfilled with explicit unknown values, compatible with existing revisions and tested on SQLite/PostgreSQL where supported. No destructive production migration in the design phase.

Security design: keep framework password hashing/session storage, CSRF, object authorization, upload quarantine and rate limits. Validate typed boundaries, redact sensitive logs, externalize secrets, bound background work and retain transaction/idempotency guards. Configuration references carry versions. No raw PAN; masked card identifiers only. No external OCR upload without an approved provider/privacy configuration. Deployment encryption, backups, RPO/RTO and monitoring remain measurable release controls.

## 10. Conținut RO/EN / Bilingual content

Move user-facing strings to typed catalogs with key parity checks. Persist language preference per user and non-sensitive pre-login preference; set document lang. Format money with Intl locale selected by user; RO: 1.234,56 RON and DD.MM.YYYY; EN: locale-consistent numbers, unambiguous dates. API stores ISO dates and decimal values independently of display. Never parse formatted currency with floating-point shortcuts. Preserve original ERP headers exactly, even when the UI is Romanian.

| RO | EN |
|---|---|
| Decont | Expense report |
| Bon fiscal | Fiscal receipt |
| Factură | Invoice |
| Tip de cheltuială | Expense category |
| CUI furnizor | Supplier tax ID |
| CUI-ul companiei pe bon | Company tax ID on receipt |
| Bază impozabilă | Taxable base |
| TVA din bon | VAT on receipt |
| TVA deductibilă | Deductible VAT |
| Plătit personal | Paid personally |
| Card de companie | Company card |
| Centru de cost | Cost center |
| Nomenclatoare | Master data |
| Asociere tranzacție | Transaction match |
| Ciornă | Draft |
| În aprobare | Awaiting approval |
| Necesită corecturi | Needs correction |
| În verificare financiară | In Finance review |
| Pregătit pentru export | Ready for export |
| Înregistrat în contabilitate | Posted |
| Salvează | Save |
| Trimite spre aprobare | Submit for approval |
| Returnează pentru corecturi | Return for correction |
| Confirmă datele | Confirm details |
| Reîncearcă | Retry |

Error examples: «Totalul calculat diferă de totalul bonului. Verifică baza și TVA pentru fiecare cotă.» / “The calculated total differs from the receipt total. Check the base and VAT for each rate.” «Fișierul nu a putut fi citit. Încarcă o imagine mai clară sau completează datele manual.» / “The file could not be read. Upload a clearer image or enter the details manually.” Display recovery only when the state actually permits it.

## 11. Livrare și acceptare / Delivery sequence

| Batch | Concrete changes planned | Acceptance gate |
|---|---|---|
| B1 Financial correctness | Rules/Posting, client validation, policy dimension guards; tests | Tolerance boundaries/FX/vehicle, N/A dimensions, 24-column parity and old revision replay |
| B2 Foundation | Typed catalogs, tokens, shared form/table states, navigation/profile | RO/EN parity, focus/keyboard/contrast, permissions and password/session tests |
| B3 Receipt/evidence | Guided editor, draft/autosave, supplier branches, evidence coverage | All conditional branches; 50/51 lines; shared evidence/new lines; offline/conflict recovery |
| B4 OCR/preview | Provider contract, geometry/confidence, review provenance, viewer | Real RO/EN varied documents, multipage grouping, null confidence, rotated overlays, failure recovery |
| B5 Operations | Manager history, Finance queues/card/ERP, analytics, notifications | Role-scoped end-to-end journeys, allocations, retry/idempotency, filtered dashboard count parity |
| B6 Governance | Masters/import/merge, audit export, retention/holds and job operations | Transaction rollback, historical references, full export, no unauthorized mutation |
| B7 Release evidence | User guides RO/EN, API/architecture/changelog, actual screenshots, Docker/CI | Full critical regression, usability/accessibility, sizing/load/security/restore evidence and owner QA |

No production implementation is changed by this design package. No current Broken/Missing row is upgraded to Done. Phase 2 checks: all 19 source pages accounted for; five business roles plus combined-role behavior specified; source IDs preserved; glossary and tokens supplied; recovery paths and acceptance owners documented. Full semantic verification and runtime acceptance remain open.

Outstanding inputs: approved ERP examples, bank formats, SMTP, hosting and load sizing. Proceed with configurable adapters and labelled provisional fixtures; do not invent production values. No new blocking question is needed to finish this design baseline.
