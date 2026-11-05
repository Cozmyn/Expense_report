# Registru sursă–regulă / Source-to-rule register

Source: attached Part03/05 v2.0 (04.11.2026). This is a traceability record, not a new workbook audit or an assertion that workbook samples are valid policy.

| Source | Observed defect/behavior | Implemented rule | Executable evidence / authority |
|---|---|---|---|
| Template E13 | Supplier registry lookup | Date-effective verified CUI lookup; unknown supplier confirmation and Finance registration | ReceiptTests; existing supplier workflow |
| Template K:L | Original document gross/currency | Preserve original decimals; reject excess precision rather than change gross | FinancialWorkflowTests.EntryNeverSilentlyRoundsOriginalDocumentGross; exact-mode HTTP tests |
| Template M:P | Printed VAT input | Printed VAT is source evidence; unknown/not-present never silently become zero | InvoiceArithmeticTests; typed TaxData.State and document treatment |
| Template Q13 | Foreign/no-CUI bypass marked OK | Explicit owner fallback or approved scoped policy with applicability reason | TaxPolicies.Resolve; policy scope/gap/retirement tests |
| Template R13/U13 | Category GL/subaccount lookup | Effective category mapping, pinned IDs/JSON/dates in accepted calculation | Posting.Find; accepted mapping snapshot and invalidation tests |
| Template S13:S99 | Local account references wrong expense rows | Map category independently of row number | BusinessTests.EveryWorkbookRowPositionUsesTheSameCategoryAccountsAndExpenseEmployee (all 87 positions, 13–99) |
| S19→R21, S29→R211, S39→R311, S49→R411, S59→R511, S69→R611, S79→R711, S89→R811 | Exact examples stated in PDF; source says at least 18 defects | None of these spreadsheet row references is executed. All 87 positions use the same typed effective mapping | Same 87-position regression; remaining defective source coordinates are not invented |
| Template T13 | Workbook branches employee/card holder | Worker is expense employee; no card-owner module | Explicit earlier owner instruction; employee Worker regression |
| Template V13 | LEFT(cost centre,5) | Explicit costCenters.erpCode, no text slicing | Posting; effective master validation |
| Template W13 | Vendor account lookup | Effective supplier CUI→ERP account version | SupplierAccount histories and snapshot mapping JSON |
| Template X13:X99 | Fixed rates per currency | Capture latest entered Finance estimate; Finance selects bank/manual/estimate final value | FinancialWorkflowTests; no-FX-reapproval owner decision D03 |
| Template Y13 | 0% base blanks/zero ambiguous | Only documented 0%-base, linked source. Never infer residual | Owner D04; zero-base evidence fixtures; VerifiedVatComponent |
| Template Z13 | 5% and single-rate RON subtraction | 5% disabled; no mixed-currency reconstruction | Explicit owner instruction; 5/19 rejection tests |
| Template AA13 | 11% base reconstruction | Original-currency printed VAT / 0.11, exact intermediate then configured rounding | InvoiceArithmeticTests; current/mixed fixtures |
| Template AB13 | 21% base reconstruction | Original-currency printed VAT / 0.21 | InvoiceArithmeticTests |
| xl/tables/table1.xml vs Template AL13 | Descriptor uses 0.19 while live formula uses 0.21 | 19% rejected; never remap historical source silently | Owner instruction; OcrReviewTests.DisabledRatesCannotBeOverriddenOrPosted |
| Template AC13 | Special +/-2 near-zero count | Every documented component counts. Rounding is separate accounting amount | Updated small-zero regressions; owner D04 |
| Template AD:AG | Flags conflate blank and zero | Typed unknown, not-present, explicit-zero, printed; distinct document exemption/outside-scope policy | Core evidence tests; policy-controlled treatment API/UI |
| Template AH:AI | RON-only variance and tolerance | Original-currency reconciliation and separate RON allocation delta | FinancialPreview; signed ±2/±2.01 fixtures |
| Template AJ:AL | +/-1 checks and stale labels | Configured Finance rounding/tolerance, current 11/21; 5/19 never allowed | Currency policy snapshots; independent tax policy authority; owner D01/D02 |
| Template AM13 | Sum reconstructed bases | Sum canonical components, shared accepted posting payload | FinancialCalculations.Accept→Posting; original/confirmed XLSX layers |
| GL Posting M1 | Fixed range sum | All active expenses, maximum 50 per owner rule | Rules.MaxLines and report totals/tests |
| Sheet8.Unprotect clear | Clear previous export output | Stateless values-only 24-column generation and immutable export batches | BusinessTests.WorkbookContainsOnlyValuesAndExactSchema |
| Sheet3.Unprotect clear | Clear input values | Confirmed reversible reset archives lines, preserves originals/history and permits guarded restore | ReportResets; ownership/confirmation/source-preservation tests and browser workflow |
| Module1 | Active-sheet/first-blank iteration | Validate every active typed record | Rules.Validate and report preflight |
| Module4 | Limited print range | Dynamic report PDF and pack, all active expenses | Existing PDF/pack browser workflows |
| Named ranges / #REF! | Broken/sample references | Reject broken references; production export rejects known sample mapping markers | FinanceAcceptance.MasterProblems and tests; configuration acceptance hash |
| VBA VerificaTVA lines1243–1263 | Binary float round and green/amber cell colors | Decimal calculation reasons; no color-based approval; no reason-only bypass | InvoiceArithmetic, Finance confirmation, immutable snapshots and preflight |

## Accepted owner deviations

5% and 19% remain disabled; negative documents use the normal flow with warnings; Worker is the expense employee; 50 active receipts; separate personal/company-card reports; no OIDC/card-owner module; FX-only Finance changes never request manager reapproval; ≤2RON rounding needs no extra attachment, with original source still mandatory.

## Sign-off

The software exposes independent Finance policy approval and configuration acceptance with actor, reason, authority, timestamp and configuration hash. This document does not impersonate a human Finance approver. Actual Accounting/ERP/corpus acceptance must be recorded by the responsible users.
