# Urmărirea cerințelor / Requirements tracking

Sursa: documentația v2 Simple Auth furnizată în conversație. „Cod prezent” înseamnă implementare în sursă, nu acceptanță sau test backend trecut. Toate rândurile necesită verificare de integrare și UAT.

| ID | Requirement | Status | Limitation / evidence |
|---|---|---|---|
| FR-EMP-001 | Create a new expense report as Draft. | Cod prezent / Source present | Api.cs, Reports.cs, main.tsx; Automated backend checks available; see verification record. |
| FR-EMP-002 | Add, edit, duplicate and delete expense lines individually up to 50 lines. | Cod prezent / Source present | Api.cs, Reports.cs, main.tsx; Automated backend checks available; see verification record. |
| FR-EMP-003 | Capture photo from mobile camera or upload JPG/PNG/HEIC/PDF; support multiple files per line. | Parțial / Partial | Camera shortcut/multiple selection not exposed; sequential uploads supported. |
| FR-EMP-004 | Run OCR/document extraction asynchronously and prefill supplier, VAT ID, document number/date, currency, total and VAT where confidence permits. | Parțial / Partial | Text OCR only; no structured prefill/confidence scoring. |
| FR-EMP-005 | Display side-by-side document preview and expense form on desktop; optimized single-column flow on mobile. | Cod prezent / Source present | Api.cs, Reports.cs, main.tsx; Automated backend checks available; see verification record. |
| FR-EMP-006 | Dynamically show REQUIRED / OPTIONAL / NOT APPLICABLE fields. | Parțial / Partial | Backend states shared; richer optional/not-applicable explanations incomplete. |
| FR-EMP-007 | Support Paid personally and Company Card payment methods. | Cod prezent / Source present | Api.cs, Reports.cs, main.tsx; Automated backend checks available; see verification record. |
| FR-EMP-008 | For Company Card require active card selection; allow card owner different from expense person. | Cod prezent / Source present | Api.cs, Reports.cs, main.tsx; Automated backend checks available; see verification record. |
| FR-EMP-009 | Show live totals: transaction currencies, estimated RON, VAT split and report total. | Parțial / Partial | RON totals available; complete multi-currency/VAT summary missing. |
| FR-EMP-010 | Submit only when all blocking validations pass. | Cod prezent / Source present | Api.cs, Reports.cs, main.tsx; Automated backend checks available; see verification record. |
| FR-EMP-011 | View status timeline, approval, Finance return reasons and ERP/closed status for own reports. | Cod prezent / Source present | Api.cs, Reports.cs, main.tsx; Automated backend checks available; see verification record. |
| FR-EMP-012 | Correct rejected/returned reports and resubmit as a new revision. | Cod prezent / Source present | Api.cs, Reports.cs, main.tsx; Automated backend checks available; see verification record. |
| FR-MGR-001 | Provide approval inbox containing reports routed to the manager. | Parțial / Partial | Assigned/delegated server scope; inbox needs additional filtering for multi-role users. |
| FR-MGR-002 | Review report summary, lines, VAT, card indicator and original attachments without downloading files. | Cod prezent / Source present | Reports.cs, Admin.cs, main.tsx; Automated backend checks available; see verification record. |
| FR-MGR-003 | Approve with one explicit action. | Cod prezent / Source present | Reports.cs, Admin.cs, main.tsx; Automated backend checks available; see verification record. |
| FR-MGR-004 | Reject/return with mandatory reason and optional line-specific comment. | Parțial / Partial | Report reason supported; line-specific comments missing. |
| FR-MGR-005 | Support manual delegation/temporary substitute and organization-based routing. | Parțial / Partial | Manual dated delegation; historical organisation routing missing. |
| FR-FIN-001 | Provide Finance queue with filters by status, employee, manager, card, supplier, aging, vendor issue, VAT issue and ERP readiness. | Parțial / Partial | Search/status only; full saved accounting filters missing. |
| FR-FIN-002 | Provide split-screen accounting review with report data/grid and document preview. | Cod prezent / Source present | Finance.cs, Reports.cs, Documents.cs; Automated backend checks available; see verification record. |
| FR-FIN-003 | Allow Finance to modify any report or accounting field with mandatory reason and immutable audit before/after. | Parțial / Partial | Expense/header editing with reason; not every accounting/report field exposed. |
| FR-FIN-004 | Run checklist/preflight validations for document, vendor, VAT, GL, local account, cost center, project, duplicate, card reconciliation, approval and ERP mapping. | Cod prezent / Source present | Finance.cs, Reports.cs, Documents.cs; Automated backend checks available; see verification record. |
| FR-FIN-005 | Return report to employee for correction with reason. | Cod prezent / Source present | Finance.cs, Reports.cs, Documents.cs; Automated backend checks available; see verification record. |
| FR-FIN-006 | Manage missing-vendor requests and assign ERP vendor number when created. | Cod prezent / Source present | Finance.cs, Reports.cs, Documents.cs; Automated backend checks available; see verification record. |
| FR-FIN-007 | Resolve duplicate candidates with reason code: duplicate / not duplicate / allowed exception. | Parțial / Partial | Allowed duplicate exceptions and archival; complete reason-code workflow missing. |
| FR-FIN-008 | Review estimated vs actual bank-settled RON for company-card transactions. | Cod prezent / Source present | Finance.cs, Reports.cs, Documents.cs; Automated backend checks available; see verification record. |
| FR-FIN-009 | Preview generated ERP posting lines before export. | Cod prezent / Source present | Finance.cs, Reports.cs, Documents.cs; Automated backend checks available; see verification record. |
| FR-FIN-010 | Generate ERP XLSX only after successful preflight. | Cod prezent / Source present | Finance.cs, Reports.cs, Documents.cs; Automated backend checks available; see verification record. |
| FR-FIN-011 | Mark ERP batch as Posted and record posting reference/date; prevent duplicate export/post. | Parțial / Partial | Unposted void supported; posted reopen/reversal not automated. |
| FR-FIN-012 | Generate official Expense Report PDF and download originals package. | Parțial / Partial | PDF generation regression and PDF/ZIP downloads tested in CI; real-document visual acceptance and signature not provided. |
| FR-CARD-001 | Maintain corporate-card master: masked PAN, owner, issuer, currency, active dates/status. | Parțial / Partial | Owner/last-four/issuer/currency metadata; richer card management missing. |
| FR-CARD-002 | Import bank statement from configurable CSV/XLSX format. | Parțial / Partial | Configurable CSV backend; XLSX/mapping profiles missing. |
| FR-CARD-003 | Auto-suggest transaction matches using card, amount/currency, date proximity and merchant similarity. | Cod prezent / Source present | Finance.cs, main.tsx; Automated backend checks available; see verification record. |
| FR-CARD-004 | Support partial, one-to-many and many-to-one matches with matched amount. | Parțial / Partial | Many-allocation backend; complete manual/undo UI missing. |
| FR-VAT-001 | Maintain effective-dated VAT rates and ERP tax mappings. | Cod prezent / Source present | Rules.cs, Posting.cs, Admin.cs; Automated backend checks available; see verification record. |
| FR-VAT-002 | Use document date to determine active VAT configuration. | Cod prezent / Source present | Rules.cs, Posting.cs, Admin.cs; Automated backend checks available; see verification record. |
| FR-VAT-003 | Support 0%, 11%, 21% active policies; 5% inactive for new transactions. | Parțial / Partial | 0/11/21 allowed and 5 blocked; future-rate activation missing. |
| FR-VAT-004 | Preserve +/-1 RON arithmetic tolerance and 0% small-value tolerance. | Parțial / Partial | Arithmetic tolerance implemented; small-zero tolerance/override equivalence pending. |
| FR-VAT-005 | Apply 50% deductibility for Fuel car and Other car expenses; 11% follows 21%-style split logic. | Parțial / Partial | 50% split code; actual golden tests not provided/executed. |
| FR-SUP-001 | Maintain local supplier master with normalized VAT ID/CUI, supplier name, ERP vendor account and active status. | Parțial / Partial | Dated normalized suppliers; single organisation only. |
| FR-SUP-002 | Create Vendor Request automatically if domestic supplier has no ERP vendor account. | Cod prezent / Source present | Rules.cs, Reports.cs, Finance.cs; Automated backend checks available; see verification record. |
| FR-SUP-003 | Offer structural and optional external VAT-ID validation (e.g., VIES where applicable). | Parțial / Partial | CUI checksum only; optional external validation missing. |
| FR-ERP-001 | Implement deterministic ERP Posting Rules Engine reproducing validated macro behavior. | Parțial / Partial | Branch engine present; legacy equivalence and golden acceptance pending. |
| FR-ERP-002 | Generate XLSX values-only workbook with worksheet name Import and exact 24-column order. | Cod prezent / Source present | Posting.cs, Finance.cs, Documents.cs; Automated backend checks available; see verification record. |
| FR-ERP-003 | Keep currently unused ERP fields configurable and disabled/defaulted until activated. | Parțial / Partial | Optional default fields/templates available; full configurable source schema missing. |
| FR-ERP-004 | Maintain immutable ERP export batches, file hashes, generation timestamp, user and report revision. | Cod prezent / Source present | Posting.cs, Finance.cs, Documents.cs; Automated backend checks available; see verification record. |
| FR-ERP-005 | Support configurable filename convention; sheet name remains fixed Import. | Parțial / Partial | Fixed safe filename; custom filename convention missing. |
| FR-AUTH-001 | Provide a simple login page with username and password. | Cod prezent / Source present | Program.cs, Sessions.cs, Admin.cs; Automated backend checks available; see verification record. |
| FR-AUTH-002 | Allow Admin to create, disable, unlock and reset users and assign one or more roles. | Cod prezent / Source present | Program.cs, Sessions.cs, Admin.cs; Automated backend checks available; see verification record. |
| FR-AUTH-003 | Support temporary password on user creation and force password change at first login or after Admin reset. | Cod prezent / Source present | Program.cs, Sessions.cs, Admin.cs; Automated backend checks available; see verification record. |
| FR-AUTH-004 | Provide forgot/reset password flow through email when SMTP is configured; Admin reset remains available as fallback. | Parțial / Partial | Admin reset only; self-service email reset missing. |
| FR-AUTH-005 | Use secure server-side session/cookie authentication with configurable idle timeout and explicit Logout. | Parțial / Partial | Server sessions/logout present; timeout absolute, not idle. |
| FR-ADM-001 | Maintain employees, users, roles, organization/manager hierarchy and delegations. | Parțial / Partial | Users/roles/manager/delegation supported; full dated hierarchy missing. |
| FR-ADM-002 | Maintain expense types, countries, document types, cost centers, projects, cards, suppliers, VAT, GL/local/subaccount mappings, FX and ERP mappings. | Parțial / Partial | Core masters supported; country/document-type masters missing. |
| FR-ADM-003 | Import selected masters from CSV/XLSX with validation preview and rollback on failed batch. | Parțial / Partial | No master bulk import. |
| FR-ADM-004 | Configure dynamic field requiredness and notification/reminder rules. | Parțial / Partial | Field rules dated; notification/reminder configuration missing. |
| FR-REP-001 | Employee dashboard shows own reports/status and reimbursable amount in process. | Parțial / Partial | Scoped counts/total; reimbursable-in-process summary incomplete. |
| FR-REP-002 | Manager dashboard shows pending approvals and aging. | Parțial / Partial | Pending counts available; aging summary missing. |
| FR-REP-003 | Finance dashboard shows queues for vendor, VAT, duplicate, card reconciliation, ERP-ready and aging. | Parțial / Partial | Basic queue; complete exception/aging dashboard missing. |
| FR-REP-004 | Provide spend reporting by employee, cost center, expense type, supplier, GL, project, card and month. | Parțial / Partial | CSV and category/status aggregations; full grouping/filtering missing. |
| FR-NOT-001 | Send email + in-app notifications on submit, reject, approve, Finance return, vendor required/resolved, ERP ready and critical failure. | Parțial / Partial | Some workflow events and job status; complete notification coverage missing. |
| FR-NOT-002 | Support configurable manager/Finance reminders and escalation. | Parțial / Partial | Reminder/escalation scheduler not implemented. |
| FR-AUD-001 | Record append-only audit events for create/edit/delete state changes, approvals, master changes, attachment events, card matches and ERP exports. | Cod prezent / Source present | Database.cs, Api.cs, Admin.cs; Automated backend checks available; see verification record. |
| FR-AUD-002 | Provide searchable audit timeline and export for authorized Finance/Auditor. | Parțial / Partial | Latest events and report timeline; complete search/export UI missing. |

## Cerințe nefuncționale / Non-functional requirements

Identity password hashing, CSRF, role/object checks, immutable application audit, upload validation, quarantine, retention metadata and language switching have code implementations. Production TLS/encryption, ASVS validation, availability, disaster recovery, performance, end-to-end observability and accessibility acceptance remain deployment/test gates. See DEPLOYMENT.md and STATUS.md.
