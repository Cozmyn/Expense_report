# PDF03 completion — phase results

## Experts and roles

Product owner; UX/content RO+EN; financial/full-stack architecture; Accounting/data integrity; front/back-office operations; QA. These are review perspectives, not a claim of independent human accountant sign-off.

## Phase 1 — audit

Reviewed all 12 PDF pages and the existing 59-group matrix/code. Retained every source coordinate and owner decision. Gaps identified in exact decimal transport, evidence states, scoped policy approval, bank charges, reset/restore, full mapping provenance/diffs and real acceptance.

## Phases 2–4 — implemented

- Immutable tax-policy drafts with a different Finance approver, dated/scoped resolution, explicit ties/gaps and controlled retirement. Owner rules remain effective before the first adopted policy period.
- Source-backed VAT states and policy-required special treatments; no invented zero-rate base or 5/19 remapping; existing vehicle category accounting preserved.
- Full accepted input/source/mapping/FX snapshots, normalized unique VAT components, scoped paginated before/after comparisons and cross-receipt calculation attribution checks.
- Explicit principal/fee/markup reconciliation and source-linked charge expenses; correction by match undo plus immutable revocation. Bank FX choices require current classification of all allocated transactions. Individual statement attachments can be reused without claiming to contain all receipts.
- Confirmed scoped reset/restore, preserving files/history and invalidating financial confirmations; posted exports remain immutable.
- Exact decimal browser transport, lossless historical JSON parsing, BigInt sums/comparison/formatting/OCR aggregation/rate serialization; source currency precision and configured allocation rounding preserved.
- Central Finance settings independent of report creation, plus contextual RO/EN controls. Latest approval estimate honors both supported Finance rate-entry paths.
- Real configuration-hashed Finance acceptance UI and production export gate; expense FX decisions never request manager reapproval.

## Phase 5 — verified

Tested runtime: b4efb248b13e68c6b049688c2d89990aa0f060ed.
CI: https://github.com/Cozmyn/Expense_report/actions/runs/42037424593

200 tests pass on each database, 9 UI tests pass, 6 desktop/phone workflows pass, TypeScript/build/dependency audit and constrained container launch pass. Backend test hosts isolate keys and explicitly disable fixture background workers; real scan/OCR workers remain exercised in browser/container gates.

## Matrix and remaining risks

59/59 mapped; 56 Done, 3 Partial for actual production master data, representative corpus and Accounting/ERP acceptance. No Missing/Broken software group. Deployment needs schema release8 and real configuration. Real users must perform and record the remaining human checks; tests do not impersonate them. No deployment to a live production environment or actual ERP posting is claimed.
