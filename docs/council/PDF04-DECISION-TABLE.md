# PDF04 — workbook posting decision table

Authority: [owner decisions](PDF04-OWNER-DECISIONS.md). Configuration is scoped to the application's single company; no new tenancy or card-owner dimension. Resolve category, VAT, supplier account, cost centre, project and settings at document date. Consume the accepted Finance calculation and source evidence, including explicit VAT applicability. Both manual and OCR-confirmed expenses enter this same contract.

| Strategy | Country group | Company VAT ID printed | Vehicle mapping | Preserved source-like Import behavior |
|---|---|---|---|---|
| FOREIGN_STANDARD | Foreign | Not applicable | No | Gross RON ledger row with configured clearing offset; no domestic VAT code or fabricated vendor. |
| FOREIGN_CAR | Foreign | Not applicable | Yes | Existing two vehicle gross allocations; default50/50 with original subaccount distinction; Finance percentage can version the allocation. |
| RO_NO_CUI | Romania | No | Either | Original gross-only row/clearing offset. Vehicle classification still reports default50; no additional recoverable VAT or split is invented. |
| RO_CUI_STANDARD | Romania | Yes | No | Per-rate documented bases with configured Q/R codes, one source-like vendor/base row and gross clearing. Preserve invoice identity and printed VAT evidence; do not create a separate ERP tax layer. |
| RO_CUI_CAR | Romania | Yes | Yes | Original per-component vehicle allocations, non-deductible VAT/account rows and vendor/clearing placement. Default50 retains the workbook half/quarter roundings. A different Finance percentage requires applicable accounting tax codes. |

Common gates: ≤50 active receipts; one report payment/card header; existing supplier ERP account for domestic company-CUI branches; current manager approval and Finance calculation/review; effective mappings; verified source; current accepted posting fingerprint. Signed amounts reverse positive debit/credit sides in the same rows. Payment method never selects vehicle percentage or substitutes the card holder for expense Worker.

EXEMPT/OUTSIDE_SCOPE/NOT_PRESENT require the existing approved explicit source policy and explanation. They preserve gross without inventing a zero-rate VAT base/code. Unknown treatment blocks acceptance. Source rounding is retained separately only under the already-approved Finance rounding policy; no credit mutation hides a discrepancy.

## Column contract

The [source manifest](PDF04-SOURCE-MANIFEST.json) records exact A:X headers. Column values, blank optional settings and original row placements remain workbook-compatible per D04-02. A/D/F/G/I/P/W/X are not populated with invented defaults. Existing explicit configuration is preserved; nonempty date/currency values are validated without reformatting. Numeric amounts must survive Excel precision exactly. Text is emitted as text, never a formula; output contains no macros.

The import rows are not declared a complete ERP ledger: the owner expressly excluded an additional ERP VAT-calculation/balancing model. Reconciliation proves source base+paidVAT+approved rounding and exported gross, preserving the workbook import behavior and leaving actual ERP acceptance to performed company checks.
