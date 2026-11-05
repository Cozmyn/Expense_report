# Verificare Finance / Finance verification — 05.11.2026

Tested runtime commit: `99e850e8435f14d4feccdcc2911fdcfd1bb0a4e6`.

[Full CI evidence — run 41991342254](https://github.com/Cozmyn/Expense_report/actions/runs/41991342254)

| Gate | Result | Evidence |
|---|---|---|
| SQLite backend | PASS — 179 tests, 0 failed | Job 127124043716 |
| PostgreSQL backend | PASS — 179 tests, 0 failed | Job 127124044273 |
| UI unit tests / TypeScript / production build / dependency audit | PASS — 5 unit tests | Both backend/frontend jobs |
| Desktop and mobile end-to-end | PASS — 4 workflows | Job 127124988785; 4 passed, 2.3 minutes |
| Container launch | PASS | Job 127124988854; read-only filesystem, non-root, dropped capabilities, no-new-privileges, memory/PID limits, repeated bootstrap, readiness/static assets/auth checks |

Browser workflows cover real scan/OCR intake and grouping/correction, individual/combined evidence, all three receipt branches, mixed VAT, manager approval, explicit Finance estimate selection and manual adjustment, base/total confirmation, preserved estimate/revision, duplicate disposition, accounting/ERP exports, PDF and report pack. The new Finance screen is exercised in Romanian and English on desktop and phone width. The `browser-results` run artifact includes screenshots `frontend-07b-finance-calculations-chromium.png` and `frontend-07b-finance-calculations-mobile.png`, plus OCR screenshots and the earlier page captures.

## Deciziile beneficiarului / Owner decisions

All five owner decisions are implemented and regression-tested. Original gross is not silently rounded on entry. Printed VAT is retained; derived bases are confirmed by Finance and used in ERP. Currency/rounding settings and final FX decisions are Finance-only. Bank matching is a proposal rather than an automatic final amount. FX-only changes preserve manager approval and the captured estimate. Normal clean supporting evidence remains mandatory; accepted rounding up to 2 RON needs no additional attachment. Negative values follow the normal flow with warnings and signed accounting treatment.

## Limite / Limits

This proves the executed software regression gates, not 100% completion or production/accounting acceptance. The PDF03 matrix has 42 Done, 13 Partial and 4 Missing groups. Separate multi-dimensional tax-policy author/approver lifecycle, remaining detailed provenance/diff/reset/fee work, exact decimal strings across every legacy read DTO, real production corpus quality and Accounting/ERP acceptance are still explicitly tracked. No legal sign-off, account numbers, real production documents or approvals have been fabricated.

Deploy using schema release 7 (`--init`) and configure real rounding account/local account mappings before exporting a rounding row. Existing posted batches remain unchanged. See [owner decisions and bilingual QA checklist](FINANCE-DECISIONS-2026-11-05.md).
