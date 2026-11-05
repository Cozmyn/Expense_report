# PDF03 completion — progress and acceptance

## Phase 1 / audit

Reviewed the existing 59-group traceability register against all 12 PDF pages and the main runtime. Remaining groups covered exact decimal transport, evidence states, controlled scoped policy approval/resolution, bank charge treatment, reversible reset, mapping reproducibility, readable diffs, formula registry and real Finance acceptance. No workbook re-audit was started.

## Phase 2 / implementation decisions

- Immutable drafts plus independent Finance approval/retirement decisions for tax policy; operational FX/currency settings remain direct Finance controls.
- Priority/specificity-based company/country/CUI/document/category/date resolution. Gaps or equal-priority ties block. Once controlled policy has been activated, retiring it cannot silently restore the owner fallback.
- Standard, exempt, outside-scope, not-present and unknown are distinct. Special treatment requires matching approved policy and source explanation; no fictitious 0%-base.
- Bank principal/fees/markup reconcile exactly; fees and markup become separate RON company-card expenses, linked to a clean statement. Principal alone drives allocation FX. Corrections use immutable revocation history after allocation undo. Signed reversals stay in the same flow.
- Reset archives active receipts/groups, never original files. Restore is confirmed, versioned, scoped, blocks non-empty targets, and invalidates financial confirmations.
- Exact decimal HTTP mode is used by the browser; legacy API clients retain numeric responses. BigInt decimal arithmetic handles browser totals, OCR aggregation, drafts, reconciliation and formatted money.
- Accepted snapshots pin input, source hashes, group regions, full mapping versions, policy decisions and FX evidence. Paginated history exposes before/after fields.
- Human Finance/ERP acceptance is recorded only through an authenticated Finance action with actual checklist completion and a current configuration fingerprint.

## Phase 3 / QA in progress

Initial expanded local suite: 193 backend tests passing. Eight UI unit tests and TypeScript/production build cover exact decimal arithmetic/rate serialization and client request controls. Desktop/phone workflows added for reset/restore, statement fee separation, calculation provenance and independent policy approval. Cross-database, real scan/OCR and container gates will be recorded after CI.

## External acceptance

Actual production master data, representative receipt corpus quality, legal/accounting policy authority and ERP import sign-off are not invented by software tests. Responsible Finance users must record the real checks in the acceptance screen. Software implementation coverage and human acceptance are tracked separately.
