# PDF03 — Implementation traceability and acceptance

Source: attached Part03/05 v2.0, 04.11.2026, all12pages. PDF SHA-256: b8cb81df5af0b739872ffed172d4cd6ef8f284e3740ccbf93425f4d08691b989.

All 59 requirement groups are mapped to implementation, source and verification. Software implementation is complete in tested runtime b4efb248b13e68c6b049688c2d89990aa0f060ed. Status remains Partial for three requirements that require actual production master-data/corpus/Accounting/ERP acceptance; no human approval is fabricated. See PDF03-COMPLETION-VERIFICATION.md.

| ID | Source | Requirement | Status | Existing evidence / implementation / next fix |
|---|---|---|---|---|
| PDF03-001 | §1 K:L | Preserve verified original gross/currency, parsed values and corrections | Done | Original Json/OCR retained; separate immutable ExpenseCalculation and accounting workbook source/confirmed bases |
| PDF03-002 | §1 M:P; §2 | Printed VAT is input evidence; do not calculate printed VAT from gross | Done | InvoiceArithmetic + existing OCR raw evidence |
| PDF03-003 | §2, §6 Y:AB | Reconstruct positive-rate bases VAT/(bps/10000) in invoice currency | Done | InvoiceArithmetic -> Finance confirmation -> canonical PostingJson -> Posting |
| PDF03-004 | §2 Y13 | 0% VAT zero; zero base must be documented or explicitly approved residual | Done | Documented zero base and clean source; no inferred residual; owner D04 |
| PDF03-005 | §2 | Preserve exact decimal intermediates and declare rounding checkpoints | Done | Versioned Finance currency exponent/rounding/tolerance and exact intermediate strings |
| PDF03-006 | §2 | No floating-point arithmetic for booked money; currency exponents | Done | ExactMoneyJson exact-v1 browser transport; BigInt decimal totals/OCR/drafts/master FX; numeric legacy API retained; precision tests |
| PDF03-007 | §2 | Negative credit notes use separate tested policy | Done | Owner D05 supersedes separate credits: same flow, sign checks, warnings and debit/credit reversal |
| PDF03-008 | §2 Q13 | Foreign/no-company-CUI treatment is explicit policy, not silent OK | Done | Explicit applicability outcome and owner policy reference in immutable financial result |
| PDF03-009 | §2 | Distinct unknown/not_present/explicit_zero/exempt/outside_scope states | Done | TaxData evidence states; separate explicit document treatments with approved policy and explanation; unknown blocks |
| PDF03-010 | §3 | Versioned company/country/document/effective-date VAT policy | Done | TaxPolicyDraft + immutable independent Finance approval/retirement, scoped effective-date rules |
| PDF03-011 | §3 | Unique typed VAT component per expense/rate with source region and verification | Done | Unique accepted VerifiedVatComponent per calculation/rate, original/derived values, source document hashes and verified regions |
| PDF03-012 | §3 | Immutable input/output hashes, precision, FX steps, policy decisions | Done | ExpenseCalculation input/context/output hashes and immutable Finance FX/policy records |
| PDF03-013 | §3, §6 | Unsupported 19% never silently maps to21%; 5% disabled by user | Done | Existing hard validation/posting/master guards; core regression tests also reject 500/1900 bps |
| PDF03-014 | §4 X13 | Per-expense manual/bank FX candidates, rate/date/source/version/evidence | Done | Typed manual/estimate/reconciled-bank proposals and immutable Finance decisions/history |
| PDF03-015 | §4 | Accounting enters approved rate or converted amount with exact precision and reason | Done | Finance-only precision-controlled rate or exact RON amount; actor and reason |
| PDF03-016 | §4 | Bank fingerprints, card/employee/merchant/date/amount matching; ambiguous alternatives | Done | Statement fingerprints, explicit scored alternatives, employee from expense; no automatic match; controlled treatment and source-linked decisions |
| PDF03-017 | §4 | No automatic winner when bank and manual amounts differ | Done | Bank match remains proposal; Finance explicitly chooses final amount |
| PDF03-018 | §4 | Fees/markup/partial settlement/reversals get distinct policy treatment | Done | BankTreatments separates principal/fees/markup; signed normal-flow reversals; partial allocations conserve principal; guarded immutable correction history |
| PDF03-019 | §4 | FX unresolved blocks final export; locking and approval records | Done | Foreign finalization requires explicit Finance decision; export requires accepted current ledger |
| PDF03-020 | §4, §6 | Manager reapproval after RON change by approved policy | Done | Owner D03: no FX-only manager reapproval; original estimate/revision preserved |
| PDF03-021 | §4 E13 | Controlled supplier registry, unknown/review outcome | Done | Existing supplier lookup, confirmation and Finance registration |
| PDF03-022 | §4 R13 | Date-effective category-to-GL mapping | Done | Existing Posting.Find(accounting,category,date) |
| PDF03-023 | §4 S13; §5 | Local account maps from category rather than wrong Excel row references | Done | Existing category map; new regression covers positions13–99 |
| PDF03-024 | §4 T13 | Worker remains expense employee by earlier explicit user decision | Done | PDF card-holder instruction superseded; regression tests expense employee |
| PDF03-025 | §4 U13 | Date-effective category-to-subaccount mapping | Done | Existing accounting mapping; include stable IDs in new ledger later |
| PDF03-026 | §4 V13 | Cost-center ERP code is explicit master data, no LEFT slicing | Done | Existing costCenters.erpCode mapping |
| PDF03-027 | §4 W13 | ERP vendor account linked to verified CUI | Done | Existing supplier master/account-period validation |
| PDF03-028 | §4 Y:AB | Do not mix RON with invoice currency for base reconstruction | Done | Original-currency reconstruction; legacy converted-RON breakdown blocks for review |
| PDF03-029 | §4 AC:AG | Typed VAT set; do not inherit ±2 near-zero anomaly as rule | Done | All documented components count; separate rounding, no near-zero anomaly |
| PDF03-030 | §4 AH:AI | Versioned original-currency and RON reconciliation deltas/status | Done | Original variance and RON rounding persisted in immutable calculations |
| PDF03-031 | §4 AJ:AL | Rate-specific tests and approved thresholds; 21% uses21, not19 | Done | 21% reconstruction and configurable Finance tolerance; boundary tests |
| PDF03-032 | §4 AM13 | Sum versioned computed bases | Done | Accepted computed bases consumed by ERP; original bases retained separately |
| PDF03-033 | §5 S19:S99 | Record defects with source coordinates, reproductions, corrected rule and approval | Done | PDF03-SOURCE-RULE-REGISTER.md records stated defective coordinates and 87-position regression; actual human sign-off tracked externally |
| PDF03-034 | §5 X13:X99 | Replace hardcoded workbook rates across currencies | Done | Latest valid Finance entry across currency settings and legacy rate masters, ordered by recorded entry time; estimate captured once, per-expense final approval retained. |
| PDF03-035 | §5 I10 | Database sums for all valid expenses, no fixed Excel range | Done | Existing active-line sums; 50-receipt report cap preserved |
| PDF03-036 | §5 Sheet8 clear | Stateless deterministic export regeneration, no stale rows | Done | Existing values-only XLSX and immutable export batches |
| PDF03-037 | §5 Sheet3 clear | Confirmed reversible reset/archive; never destroy originals | Done | ReportResets confirmed scoped archive/restore, source preservation, match/state/empty-target guards and tests/UI |
| PDF03-038 | §5 Module1 | Validate all active records; no first-blank-country/active-sheet assumption | Done | Existing per-line validation with explicit country |
| PDF03-039 | §5 Module4 | Output includes all report expenses, no A1:Q50 cutoff | Done | Existing dynamic report PDF/export |
| PDF03-040 | §5 named ranges/master data | Discard #REF! and sample placeholders; production lists validated | Partial | Software complete: broken-reference/production sample guards and configuration-hashed acceptance. Actual production master-data validation by responsible Finance remains pending. |
| PDF03-041 | §6.1 | Separate OCR_RAW/HUMAN_VERIFIED/ACCOUNTING_OVERRIDE money/source evidence | Done | Original OCR and human Json retained, separate accepted Accounting calculation layer |
| PDF03-042 | §6.2 | Resolve policy by company/country/CUI/kind/category/date | Done | Company/country/CUI/document/category/date resolution; priority/specificity, explicit gaps/ties, no fallback after adoption/retirement; earlier documents retain owner rules before the first effective date. |
| PDF03-043 | §6.4 | Compare rounded bases + preserved paid VAT against printed gross | Done | Live preview/confirmation compare derived bases plus printed VAT with gross |
| PDF03-044 | §6.5 | Convert only after original-currency calculation; preserve RON differences | Done | Original-currency reconstruction then approved FX and separate RON rounding |
| PDF03-045 | §6.6 | Stable mapping IDs/versions and reproducible payload shared by approval/export | Done | Accepted Result pins full masters, supplier accounts, FX proposals/decisions, currency versions, tax policy author/approver and source snapshots |
| PDF03-046 | §6.7 | Accepted snapshot includes input/source/policy/mapping/FX/output/actor/version/time | Done | ExpenseCalculation persisted with source/mapping/policy/FX context and hashes |
| PDF03-047 | §7 VAT001/002/004/005/006/008 | Current/mixed/zero/mismatch/foreign-currency mathematical fixtures | Done | InvoiceArithmeticTests, including original-currency and zero-evidence cases |
| PDF03-048 | §7 VAT003/007 | 5% sample pass superseded; 19% rejected | Done | Explicit user decision takes precedence; both tested as unsupported |
| PDF03-049 | §7 VAT009/010/011/012 | Unknown/no-CUI/foreign/credit applicability and authority | Done | Unknown blocked; explicit foreign/no-CUI outcome; signed normal-flow owner authority |
| PDF03-050 | §7–8 policy register | Rounding/tolerance, deductibility, 0%, FX precedence/fees/reapproval need real sign-off | Partial | Software complete: independent scoped tax-policy approval, authority register and production acceptance gate. Actual Accounting/ERP sign-off on real policies/data remains pending. |
| PDF03-051 | §8–11 formulas E:AM | Maintain source-to-rule registry; do not execute Excel in production | Done | PDF03-SOURCE-RULE-REGISTER.md links E:AM/formula defects/macros/named ranges to services, tests and owner deviations |
| PDF03-052 | §11 VerificaTVA | Do not turn workbook green/amber colors into approval; exact decimal recon | Done | Typed decimal reasons and Finance confirmations; blocked calculations cannot be forced valid by color/boolean/reason |
| PDF03-053 | §12 preview | Scoped provisional calculate-preview endpoint and bilingual financial UI | Done | Scoped calculation preview plus bilingual FinancialWorkspace |
| PDF03-054 | §12 verify/history | Transactional accepted finance snapshot and readable version/source diffs | Done | Scoped paginated CalculationHistory with before/after/source/policy/mapping diffs and bilingual comparison UI |
| PDF03-055 | §12 policy author/approve | Draft policy and controlled activation with separation of duties | Done | Draft policy, different Finance approver, immutable decisions, controlled retirement and overlap checks |
| PDF03-056 | §12 FX APIs/history | Manual/bank proposals, Accounting decision and histories | Done | FX proposals/decisions/history endpoints and Finance workspace |
| PDF03-057 | §12 invalidation | Changing inputs invalidates calculation/dependent export candidates | Done | Input, evidence, FX and mapping/policy context invalidate accepted calculations |
| PDF03-058 | §12 exceptions | No is_valid=true bypass; named override policy + actor/reason | Done | Reason-only VAT override rejected; invalid calculations cannot be forced valid |
| PDF03-059 | §12 acceptance | Golden/property tests, no float, provenance, Finance approval before PDF04 | Partial | 200 backend tests per database, 9 UI tests, 6 real desktop/mobile workflows and container gates PASS (run42037424593). Actual representative-corpus/Finance acceptance remains pending before PDF04 can be declared accepted. |

## Phase closure and evidence

1. **Audit:** all 12 PDF pages and the existing project reviewed; all 59 groups retained; no workbook re-audit.
2. **Policy and data:** immutable drafts, independent Finance approval, date/scope resolution, typed evidence and normalized source components implemented.
3. **Operations:** reproducible snapshots/diffs, bank principal/fees/markup and correction, reversible reset/restore, production Finance acceptance gate implemented.
4. **RO/EN UI and precision:** central Finance settings plus contextual controls, source-preserving decimal transport/parser/arithmetic, guided special treatment, comparison and classification forms implemented.
5. **QA:** [run42037424593](https://github.com/Cozmyn/Expense_report/actions/runs/42037424593) passed 200 tests on SQLite and 200 on PostgreSQL, 9 UI tests, 6 desktop/mobile workflows, production build/dependency audit and constrained container launch. See [completion verification](PDF03-COMPLETION-VERIFICATION.md).

**Traceability coverage: 59/59 groups mapped. Status: 56 Done, 3 Partial (real external acceptance), 0 Missing, 0 Broken.** This does not mean that a real accountant, production corpus or ERP integration has been signed off by the coding agent.

No requirement was silently removed. Owner-approved deviations remain explicit: 5%/19% disabled, signed same-flow documents, expense employee Worker, no card-owner/OIDC module, 50 active receipts, separate personal/card reports, no manager reapproval on FX, documented zero base and no additional attachment for ≤2RON rounding. Tax-policy approval is separate configuration governance by another Finance user; it does not reopen manager approval.

Remaining closure actions are only actual human/production acceptance for PDF03-040, PDF03-050 and PDF03-059. The application exposes authenticated Finance acceptance, records checks actually performed against the current configuration hash and blocks production export without it. Do not mark the PDF04 business acceptance gate closed until responsible Finance records that sign-off.
