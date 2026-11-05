# PDF03 completion — verification / verificare

Date: 05.11.2026.
Tested runtime: **b4efb248b13e68c6b049688c2d89990aa0f060ed**.
[Passing CI run42037424593](https://github.com/Cozmyn/Expense_report/actions/runs/42037424593).

| Gate | Result | Evidence |
|---|---|---|
| SQLite backend | PASS: 200, zero failed/skipped | Job127279084678 |
| PostgreSQL backend | PASS: 200, zero failed/skipped | Job127279085056 |
| UI unit / TypeScript / production build / dependency audit | PASS: 9 UI tests | Both backend/frontend jobs |
| Desktop + phone browsers | PASS: 6 workflows, 3.9 minutes | Job127280570348 |
| Constrained container startup | PASS | Job127280570402 |

## What the tests prove

The inherited OCR/source-group/receipt-to-ERP workflows still work. New flows exercise reversible reset and restoration without losing originals; source-linked bank principal/fees/markup; explicit Finance bank FX with preserved estimate/manager revision; accepted calculation provenance and comparison; draft/independent policy approval; and the central Finance configuration screen on desktop and phone widths.

Backend cases cover owner rules, disabled rates, signed amounts and accounting sides, scoped policy resolution/overlap/ties/gaps/future adoption/retirement, same-author/unauthorized rejection, special treatment without invented 0%-base, immutable normalized evidence/mapping snapshots, stale and cross-receipt calculation rejection, exact monetary HTTP compatibility and source precision, statement split/undo and individual statement evidence, reset ownership/confirmation/restore conflicts, production sample-reference protection, real acceptance requirements/configuration hashes, and latest-rate precedence across both supported Finance entry paths.

UI tests preserve sums beyond JavaScript integer precision, signed RO/EN formatting, exact rate JSON, lossless legacy money tokens/counts/prototype safety and existing CSRF request behavior.

The browser-results artifact contains existing OCR/page captures plus:
- 01-reset-and-restore.png
- 02-bank-charge-classification.png
- 03-calculation-provenance.png
- 04-policy-draft.png
- 05-policy-independent-approval.png
- 06-central-finance-settings.png

## Before → after

| Before | After |
|---|---|
| Currency controls without a scoped tax-policy lifecycle | Drafts, independent Finance approval, authority/date/scope and explicit ambiguity/gap controls |
| Calculation history mostly hashes | Pinned source/mapping/FX data and readable version differences |
| Bank totals could hide fees/markup | Explicit principal/charges and source-linked RON expenses with reversible correction |
| No whole-report reversible reset | Confirmed archive/guarded restore, originals retained |
| Legacy JSON could lose decimal precision in the browser | Exact monetary strings, lossless snapshot parsing and BigInt financial operations |
| Configuration only reachable contextually | Dedicated Finance settings and contextual controls |
| No recorded production configuration acceptance | Authenticated, current-hash Finance/ERP acceptance required for production export |

## Acceptance limits

**59/59 requirements mapped; 56 Done, 3 Partial (external human acceptance), 0 Missing, 0 Broken.**

Software mechanisms and regression gates are implemented. Actual production reference data, representative receipt corpus quality and real Finance/ERP/legal-policy sign-off remain pending under PDF03-040/050/059. No real human approval, real ERP import or live production deployment is claimed. PDF04 must not be declared business-accepted before that Finance gate is recorded.

## Owner QA / verificare beneficiar

1. Run deployment --init for schema release8; configure real accounts/categories/currencies/company data.
2. As Finance, open Configurare Finance / Finance settings without a report; verify currencies and author a correctly scoped policy. Another Finance user approves; the author cannot.
3. Enter a receipt with 0/11/21% VAT; compare original/derived bases and gross, test unknown/mismatch/disabled rates, and verify source linkage. Signed receipts retain the same flow and warnings.
4. Confirm an expense FX adjustment: estimate and manager revision stay unchanged.
5. Classify statement principal/fees/markup against a clean supporting statement, allocate only principal, then verify fee expenses and calculation differences. Undo allocation before correcting classification.
6. Reset a draft with typed report number/reason; restore into an empty report; original files remain.
7. Perform actual corpus/configuration/ERP checks and record the real Finance acceptance. Unaccepted or changed production configuration must block export.

[API and migration](API-03.md) · [Architecture](ADR-03.md) · [RO/EN guide](../USER-GUIDE-RO-EN.md) · [Traceability](BLUEPRINT-03-REQUIREMENTS.md) · [Source register](PDF03-SOURCE-RULE-REGISTER.md).
