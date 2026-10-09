# Închiderea auditului / Audit closeout

2026-10-09 · Council: Architecture, QA, Compliance/Data, Product Ownership.

**Static audit package closed; live acceptance remains open.** This closes the source/code review and prioritization deliverable, not the full requested runtime/UI/database/security audit. The remaining verification work below is an explicit Phase 3/release gate. No requirement has been accepted merely because its module exists. Read alongside PHASE-1-AUDIT.md and PHASE-2-MATRIX.csv.

## Inventar module / Module review register

| Module | Implementation inspected | Audit disposition and acceptance gate |
|---|---|---|
| Authentication/profile | Program.cs, PasswordRecovery.cs, Admin.cs; API catalogue | Cookie login/reset/change APIs exist; missing profile UX. Verify lockout, session revocation, CSRF, expiry and combined-role permissions in live deployment. |
| Employee reports | Api.cs, Reports.cs, Domain.cs, ReceiptModal.tsx | Multiple lines, drafts, revisions, dynamic rules exist. Finish guided forms, autosave conflicts, policy labels and full receipt/export parity. |
| Evidence/OCR | ReceiptFiles.cs, DocumentExtraction.cs, DocumentPreviews.cs, CapturePanel.tsx | Real extraction and quarantine checks exist. Add confidence/geometry/type detection, correction provenance, drag/drop and preview controls. |
| Manager workflow | Reports.cs, Api.cs, FinanceRouting.cs | Bound revisions, decisions, correction and routing exist. Verify all mutation paths invalidate approval when required; add dedicated history and accessible review. |
| Finance/ERP | Finance.cs, Posting.cs, Documents.cs, ReceiptAccountingExport.cs | Blocking validation/export batches exist. Tolerance defect remains. Require exact source-to-24-column mapping, deterministic revision replay and company acceptance. |
| Supplier management | ReceiptFiles.cs, SupplierValidation.cs, Finance.cs | Lookup, unknown supplier and resolution paths exist. Verify outage/override persistence, effective dates, deep links and export unblocking. |
| Company cards | Finance.cs, SpreadsheetImports.cs, Domain.cs | Import/matching exists. Add durable statement batch lineage; test many-to-many allocations, reversal, duplicate imports and actual RON settlement. |
| Users/organization | Admin.cs, EmployeeImports.cs, PlatformFeatures.cs | CRUD, delegation and effective assignments exist. Test hierarchy cycles, inactive/delegated users, transactional imports and role revocation. |
| Master data/settings | Admin.cs, SpreadsheetImports.cs, Rules.cs | Typed business validation over generic master records exists. Complete merge/history/export UX and prove historical financial rule reproducibility. |
| Notifications/jobs | NotificationPolicies.cs, Operations.cs, Api.cs | Channels/reminders/retry are implemented. Verify delivery failure, disabled channels, concurrent workers and delegation-aware reminders; no duplicate sends. |
| Analytics | Documents.cs, PlatformFeatures.cs | Reporting projections/CSV exist. Verify every requested dimension and denominator, locale, authorization and paging. |
| Audit/history | Domain.cs, Admin.cs, PlatformFeatures.cs | Audit records and revision snapshots exist. Remove silent export truncation; add human-readable differences and minimized request context. |
| Retention/security/operations | Database.cs, SchemaUpgrades.cs, Program.cs, deployment docs, CI | Separate application guarantees from deployment controls. Restore, encryption, load and legal-hold lifecycle acceptance remain open. |

## Additional test hypotheses from closeout

These are investigation items, not confirmed defects: reminder age currently derives from report Updated (check whether edits postpone SLA); reminder targets use stored manager (verify effective delegate handling); workflow consequences of archiving a Finance-edited line require explicit tests; preview-to-confirm import hashes must have a defined provenance contract; row/version conflict handling must preserve unsaved corrections. Track as QA-C01..QA-C05 respectively.

## Evidence boundary

The original initial matrix remains immutable. The Phase 2 matrix references every one of its 1,579 IDs, preserves status, and adds a design workstream, delivery batch and acceptance gate. Automated workstream allocation is a routing aid, **not individual semantic verification**. Compound rows and repeated references remain visible. No automated status upgrade to Done.

Fresh Phase 1 evidence remains five passing frontend tests and a successful frontend build; historical backend/Playwright CI is documented in PHASE-1-AUDIT.md. This closeout adds no new runtime test result. Browser-based visual review, assistive technology review, a live database audit, full endpoint permission coverage and production measurements remain outstanding. No finding is dropped to make the phase appear complete.

## Handoff

Highest priority: AUD-02/03/04 accounting and policy consistency; then AUD-05/06/07 OCR, AUD-08/09 integrity and retention; remaining UX/admin work follows with per-batch acceptance. D-036/OI-005 ERP samples and OI-001/003/004/006 deployment inputs remain external acceptance dependencies. They do not block the Phase 2 design.
