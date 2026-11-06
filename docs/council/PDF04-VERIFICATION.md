# PDF04 — final verification / verificare finală

06.11.2026. Tested implementation/CI commit: **355a3bf43838854d1b4a8585a3fe4e3b7d053a78**.
[Passing CI run 42289410204](https://github.com/Cozmyn/Expense_report/actions/runs/42289410204).

| Gate | Result | Job |
|---|---|---|
| SQLite backend |241 passed,0 failed/skipped |128138588826 |
| PostgreSQL backend |241 passed,0 failed/skipped |128138589557 |
| UI unit / TypeScript / production build / dependency audit |9 UI tests passed; build/type/audit passed | Both backend/frontend jobs |
| Desktop and phone browsers |6 workflows passed (4.0 minutes) |128140277201 |
| Constrained container startup and repeatable init |PASS |128140276988 |

The final documentation-only closure commit does not change this tested runtime.

## Five-phase closure

| Phase | Expert perspectives | Findings / decisions | Concrete changes | Verification / remaining risk |
|---|---|---|---|---|
| 1 Audit | Product Owner, Accounting/data, Architect | All11 PDF pages mapped; confirmed source checksum,18 GL mappings and24 headers; identified posting provenance, retry, permissions and dashboard gaps. | Initial100-item audit and exact inactive source manifest. | Targeted workbook confirmation only; no macros executed or full Excel re-audit. |
| 2 Contract/design | Accounting/ERP, Back-office UX, Security, RO/EN content | Owner chose Finance-only Excel, source-like Import without another ERP VAT model, editable default50 vehicle treatment independent of payment. | D04 decision register, branch table, API04, ADR04, guided Finance controls. | Conflicting source proposals explicitly superseded; no invented account numbers/dimensions/approval dates. |
| 3 Backend | Financial engine, full-stack/database, Security | Generate unchanged cells and typed provenance together; fail closed on stale input and preserve historical output. | Posting/PostingModels/PostingWorkflow/VehicleDeductibility; financial source/hash correction; Finance/Documents/Reports integration; DB constraints/triggers and schema9. | Both database suites and immutable/retry/role/migration tests; actual ERP acceptance remains human work. |
| 4 Interface | Back-office/front-office UX, RO/EN content | Calm summary→lines→source→A:X flow; Finance owns configuration; Auditors retain source/PDF without Excel. | PostingPanels.tsx, main.tsx and styles; percentage history, source viewer, posting decisions, frozen preview and accounting dashboard. | Desktop/mobile workflow gates and screenshots; automated checks are not a full independent accessibility certification. |
| 5 QA/release | QA, Product Owner, release engineering | Preserve working OCR/manager/Finance flows; distinguish generated regression files from actual approved ERP outputs. | Pdf04PostingTests,14 fixture files, expanded browser workflow, CI reference artifacts and updated docs/matrix. |241 tests per database,9 UI tests, build/audit/container and browser evidence recorded below. Real company/ERP acceptance is not fabricated. |



## What the evidence proves

The original receipt/OCR/manager/Finance/export workflows still pass. Added cases cover source-preserving VAT bases, exact A:X cells and numeric/text types, default50/payment independence, editable0/25/75/100 vehicle percentages with explicit tax codes, signed same-flow rows, original default odd-cent half/quarter rounding,50/51 receipt boundary and serializer output beyond999 rows, stale mapping/fingerprint rejection, frozen historical preview, business retry with a new HTTP key, immutable normalized calculation/FX/VAT/source/mapping trace, exact once-per-expense SQL dashboard totals and repeatable legacy-preserving schema9.

Permission tests deny Employee/Admin percentage changes, including whitespace around codes through the general master route. Auditor source/PDF access remains available while both accounting XLSX downloads are forbidden. Malformed vehicle tax-code values produce validation instead of a server error. Date/currency and Excel precision checks reject bad values without changing source amounts. Canonical hashes survive database decimal-scale changes.

Desktop/mobile workflows verify saved posting lines, source viewer, immutable preview, percentage settings and export retry alongside real ClamAV/Tesseract/PDF intake. The mobile heading/action overflow is fixed with wrapping and the390-pixel viewport assertion remains enforced.

## Review artifacts

CI verification artifacts contain12 valid **SYNTHETIC-NOT-FOR-PRODUCTION** reference XLSX files, fixture JSON and calculation/trace JSON. The14-case pack includes two explicit disabled5% rejection cases rather than fabricated successful exports. Actual Accounting/ERP approval dates remain null.

Browser artifacts include:
- pdf04-accounting-overview.png
- pdf04-source-linked-posting.png
- pdf04-vehicle-percentage.png
plus existing OCR/review/approval captures and traces.

The CI setup delay was an Azure Ubuntu mirror throughput issue. Setup now uses primary signed-package mirrors, bounded download stages and failure diagnostics. No application test, source scan or dependency was skipped.

## Before → after

| Before | After |
|---|---|
| Auditor could download accounting XLSX | Finance-only XLSX at UI and API; Auditor evidence/PDF and read-only trace retained |
| Reconstruction could replace printed VAT base | Printed source base/VAT retained after arithmetic checks |
| Vehicle split hardcoded50 | Finance-only dated percentage versions, default50, independent of payment; non50 tax codes explicit |
| String-only posting result | Exact A:X plus typed strategy/source/calculation/FX/VAT/mapping trace and stored rows |
| Request-key-only retry protection | Equivalent business retry reuses the same batch/file |
| Preview recalculated from current masters | Saved active batch preview uses immutable historical rows |
| No normalized accounting dashboard | Paged batches and exact SQL once-per-expense department/vendor/category aggregates |
| Monetary scale could create false stale hashes | Canonical financial/posting fingerprints survive database round-trips |
| No explicit Excel precision/date/currency boundary | Reject invalid/nonrepresentable values instead of silently changing them |
| Generic policy-code whitespace bypass | Normalized permission guard with regression cases; malformed tax-code input returns validation |
| Preview heading overflowed on phone | Wrapping actions with390-pixel viewport regression retained |
| Combined setup could stall without a clear stage | Separate bounded dependency stages, primary Ubuntu mirrors and setup-failure diagnostics; all gates retained |



## Traceability and acceptance limits

**100/100 source obligations mapped:98 Done (implementation),2 Partial (actual human/ERP acceptance),0 Missing,0 Broken software items identified in this review.**

[Current matrix](PDF04-REQUIREMENTS.md), [owner decisions](PDF04-OWNER-DECISIONS.md), [branch table](PDF04-DECISION-TABLE.md), [source manifest](PDF04-SOURCE-MANIFEST.json), [API/migration](API-04.md), [architecture](ADR-04.md), [RO/EN user guide](../USER-GUIDE-RO-EN.md).

This completes the PDF04 software slice under D04-01/02/03. It does not claim that every earlier premium-platform requirement, actual company configuration, live ERP import or production deployment is accepted. Responsible Finance must perform and record the real checks; synthetic fixtures do not impersonate human approval. Production acceptance remains gated.

## Owner QA / verificare beneficiar

1. Upgrade a copy of the database with init/schema9 twice; existing report files and export hashes must remain unchanged.
2. As Finance, open Configurare Finance → Deductibilitate auto. Confirm default50; save a dated test percentage with a reason. As Employee/Admin/Auditor without Finance, the mutation must be forbidden. For non50, missing applicable VAT codes must block export, not reuse50 codes silently.
3. Use a source receipt with100.01 base and21 paidVAT, gross121.01. Validation checks the arithmetic, while the accepted base and Import amount stay100.01. Inspect original source and verified calculation.
4. Use a vehicle receipt at default50, including odd cents and personal/company-card reports separately. Check the original half/quarter row pattern and same expense employee Worker. No additional ERP VAT rows/model should appear.
5. Complete Finance review; inspect document→export reconciliation, source/mapping links and exact A:X. Reject with a reason, correct/review again and export. FX-only changes must preserve manager revision.
6. Retry export with a different request key after a simulated lost response: same batch ID/file hash, one batch. Change a mapping before generation: stale export must block. Change a master after generation: saved preview/file must retain original values.
7. As Auditor, view the original and PDF; direct accounting-XLSX URLs must return403. As Finance, verify both accounting downloads.
8. In Situație contabilă, filter a date range, inspect saved batch lines and compare department/vendor/category totals. Each source expense counts once; void/reversed batches are excluded. Existing Finance queue handles source exceptions.
9. Reopen reference XLSX artifacts: exactly one Import worksheet and24 headers; numeric cells exact and no formulas/macros. Synthetic files are not actual approved ERP imports.
10. Perform and record actual company/ERP checks separately. Do not substitute test users or generated dates for real Finance acceptance. No live production deployment is claimed by this delivery.

