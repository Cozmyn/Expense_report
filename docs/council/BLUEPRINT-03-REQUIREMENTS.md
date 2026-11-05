# PDF 03 — Requirements and initial disposition

Source: attached Part03/05 v2.0, 04.11.2026, all12pages. PDF SHA-256: b8cb81df5af0b739872ffed172d4cd6ef8f284e3740ccbf93425f4d08691b989.

Status reflects implementation of owner decisions D01–D05. Broader PDF03 gaps are deliberately retained; this is not a claim of 100% PDF coverage. See FINANCE-DECISIONS-2026-11-05.md for the current rules and QA steps.

| ID | Source | Requirement | Status | Existing evidence / implementation / next fix |
|---|---|---|---|---|
| PDF03-001 | §1 K:L | Preserve verified original gross/currency, parsed values and corrections | Done | Original Json/OCR retained; separate immutable ExpenseCalculation and accounting workbook source/confirmed bases |
| PDF03-002 | §1 M:P; §2 | Printed VAT is input evidence; do not calculate printed VAT from gross | Done | InvoiceArithmetic + existing OCR raw evidence |
| PDF03-003 | §2, §6 Y:AB | Reconstruct positive-rate bases VAT/(bps/10000) in invoice currency | Done | InvoiceArithmetic -> Finance confirmation -> canonical PostingJson -> Posting |
| PDF03-004 | §2 Y13 | 0% VAT zero; zero base must be documented or explicitly approved residual | Done | Documented zero base and clean source; no inferred residual; owner D04 |
| PDF03-005 | §2 | Preserve exact decimal intermediates and declare rounding checkpoints | Done | Versioned Finance currency exponent/rounding/tolerance and exact intermediate strings |
| PDF03-006 | §2 | No floating-point arithmetic for booked money; currency exponents | Partial | Decimal-string editor submissions and decimal backend are integrated; all legacy/read DTO decimal representations still need a complete exact-string wire-contract migration |
| PDF03-007 | §2 | Negative credit notes use separate tested policy | Done | Owner D05 supersedes separate credits: same flow, sign checks, warnings and debit/credit reversal |
| PDF03-008 | §2 Q13 | Foreign/no-company-CUI treatment is explicit policy, not silent OK | Done | Explicit applicability outcome and owner policy reference in immutable financial result |
| PDF03-009 | §2 | Distinct unknown/not_present/explicit_zero/exempt/outside_scope states | Partial | Core typed evidence states; UI/database policy integration missing |
| PDF03-010 | §3 | Versioned company/country/document/effective-date VAT policy | Missing | Operational Finance currency versions are implemented. Separate multi-dimensional VAT-policy author/approver lifecycle remains outstanding; owner decisions are not fabricated tax sign-off. |
| PDF03-011 | §3 | Unique typed VAT component per expense/rate with source region and verification | Partial | Integrated Finance calculation/FX/API/UI/export layer is implemented; see decision document. Original requirement still has broader provenance/diff/policy/acceptance work; retained Partial. |
| PDF03-012 | §3 | Immutable input/output hashes, precision, FX steps, policy decisions | Done | ExpenseCalculation input/context/output hashes and immutable Finance FX/policy records |
| PDF03-013 | §3, §6 | Unsupported 19% never silently maps to21%; 5% disabled by user | Done | Existing hard validation/posting/master guards; core regression tests also reject 500/1900 bps |
| PDF03-014 | §4 X13 | Per-expense manual/bank FX candidates, rate/date/source/version/evidence | Done | Typed manual/estimate/reconciled-bank proposals and immutable Finance decisions/history |
| PDF03-015 | §4 | Accounting enters approved rate or converted amount with exact precision and reason | Done | Finance-only precision-controlled rate or exact RON amount; actor and reason |
| PDF03-016 | §4 | Bank fingerprints, card/employee/merchant/date/amount matching; ambiguous alternatives | Partial | Integrated Finance calculation/FX/API/UI/export layer is implemented; see decision document. Original requirement still has broader provenance/diff/policy/acceptance work; retained Partial. |
| PDF03-017 | §4 | No automatic winner when bank and manual amounts differ | Done | Bank match remains proposal; Finance explicitly chooses final amount |
| PDF03-018 | §4 | Fees/markup/partial settlement/reversals get distinct policy treatment | Partial | Integrated Finance calculation/FX/API/UI/export layer is implemented; see decision document. Original requirement still has broader provenance/diff/policy/acceptance work; retained Partial. |
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
| PDF03-033 | §5 S19:S99 | Record defects with source coordinates, reproductions, corrected rule and approval | Partial | Integrated Finance calculation/FX/API/UI/export layer is implemented; see decision document. Original requirement still has broader provenance/diff/policy/acceptance work; retained Partial. |
| PDF03-034 | §5 X13:X99 | Replace hardcoded workbook rates across currencies | Done | Latest effective Finance currency estimate captured at entry; controlled per-expense final value |
| PDF03-035 | §5 I10 | Database sums for all valid expenses, no fixed Excel range | Done | Existing active-line sums; 50-receipt report cap preserved |
| PDF03-036 | §5 Sheet8 clear | Stateless deterministic export regeneration, no stale rows | Done | Existing values-only XLSX and immutable export batches |
| PDF03-037 | §5 Sheet3 clear | Confirmed reversible reset/archive; never destroy originals | Partial | Integrated Finance calculation/FX/API/UI/export layer is implemented; see decision document. Original requirement still has broader provenance/diff/policy/acceptance work; retained Partial. |
| PDF03-038 | §5 Module1 | Validate all active records; no first-blank-country/active-sheet assumption | Done | Existing per-line validation with explicit country |
| PDF03-039 | §5 Module4 | Output includes all report expenses, no A1:Q50 cutoff | Done | Existing dynamic report PDF/export |
| PDF03-040 | §5 named ranges/master data | Discard #REF! and sample placeholders; production lists validated | Partial | Integrated Finance calculation/FX/API/UI/export layer is implemented; see decision document. Original requirement still has broader provenance/diff/policy/acceptance work; retained Partial. |
| PDF03-041 | §6.1 | Separate OCR_RAW/HUMAN_VERIFIED/ACCOUNTING_OVERRIDE money/source evidence | Done | Original OCR and human Json retained, separate accepted Accounting calculation layer |
| PDF03-042 | §6.2 | Resolve policy by company/country/CUI/kind/category/date | Missing | Operational Finance currency versions are implemented. Separate multi-dimensional VAT-policy author/approver lifecycle remains outstanding; owner decisions are not fabricated tax sign-off. |
| PDF03-043 | §6.4 | Compare rounded bases + preserved paid VAT against printed gross | Done | Live preview/confirmation compare derived bases plus printed VAT with gross |
| PDF03-044 | §6.5 | Convert only after original-currency calculation; preserve RON differences | Done | Original-currency reconstruction then approved FX and separate RON rounding |
| PDF03-045 | §6.6 | Stable mapping IDs/versions and reproducible payload shared by approval/export | Partial | Integrated Finance calculation/FX/API/UI/export layer is implemented; see decision document. Original requirement still has broader provenance/diff/policy/acceptance work; retained Partial. |
| PDF03-046 | §6.7 | Accepted snapshot includes input/source/policy/mapping/FX/output/actor/version/time | Done | ExpenseCalculation persisted with source/mapping/policy/FX context and hashes |
| PDF03-047 | §7 VAT001/002/004/005/006/008 | Current/mixed/zero/mismatch/foreign-currency mathematical fixtures | Done | InvoiceArithmeticTests, including original-currency and zero-evidence cases |
| PDF03-048 | §7 VAT003/007 | 5% sample pass superseded; 19% rejected | Done | Explicit user decision takes precedence; both tested as unsupported |
| PDF03-049 | §7 VAT009/010/011/012 | Unknown/no-CUI/foreign/credit applicability and authority | Done | Unknown blocked; explicit foreign/no-CUI outcome; signed normal-flow owner authority |
| PDF03-050 | §7–8 policy register | Rounding/tolerance, deductibility, 0%, FX precedence/fees/reapproval need real sign-off | Missing | Operational Finance currency versions are implemented. Separate multi-dimensional VAT-policy author/approver lifecycle remains outstanding; owner decisions are not fabricated tax sign-off. |
| PDF03-051 | §8–11 formulas E:AM | Maintain source-to-rule registry; do not execute Excel in production | Partial | Integrated Finance calculation/FX/API/UI/export layer is implemented; see decision document. Original requirement still has broader provenance/diff/policy/acceptance work; retained Partial. |
| PDF03-052 | §11 VerificaTVA | Do not turn workbook green/amber colors into approval; exact decimal recon | Partial | Integrated Finance calculation/FX/API/UI/export layer is implemented; see decision document. Original requirement still has broader provenance/diff/policy/acceptance work; retained Partial. |
| PDF03-053 | §12 preview | Scoped provisional calculate-preview endpoint and bilingual financial UI | Done | Scoped calculation preview plus bilingual FinancialWorkspace |
| PDF03-054 | §12 verify/history | Transactional accepted finance snapshot and readable version/source diffs | Partial | Integrated Finance calculation/FX/API/UI/export layer is implemented; see decision document. Original requirement still has broader provenance/diff/policy/acceptance work; retained Partial. |
| PDF03-055 | §12 policy author/approve | Draft policy and controlled activation with separation of duties | Missing | Operational Finance currency versions are implemented. Separate multi-dimensional VAT-policy author/approver lifecycle remains outstanding; owner decisions are not fabricated tax sign-off. |
| PDF03-056 | §12 FX APIs/history | Manual/bank proposals, Accounting decision and histories | Done | FX proposals/decisions/history endpoints and Finance workspace |
| PDF03-057 | §12 invalidation | Changing inputs invalidates calculation/dependent export candidates | Done | Input, evidence, FX and mapping/policy context invalidate accepted calculations |
| PDF03-058 | §12 exceptions | No is_valid=true bypass; named override policy + actor/reason | Done | Reason-only VAT override rejected; invalid calculations cannot be forced valid |
| PDF03-059 | §12 acceptance | Golden/property tests, no float, provenance, Finance approval before PDF04 | Partial | Integrated Finance calculation/FX/API/UI/export layer is implemented; see decision document. Original requirement still has broader provenance/diff/policy/acceptance work; retained Partial. |

## Current execution and validation

1. Source audit and 59-group extraction completed; original source coordinates retained.
2. Owner D01–D05 decisions recorded; conflicting provisional proposals superseded explicitly.
3. Live calculations, immutable ledgers, Finance FX decisions, invalidation and migration implemented.
4. RO/EN Finance workspace, signed receipt editing and accounting export connected.
5. Local database/unit and UI regression execution completed; CI run 41991342254 passed: 179 tests per database, 5 UI unit tests, 4 browser workflows and container launch. See BLUEPRINT-03-VERIFICATION.md. Actual Finance/ERP acceptance remains external.

The five owner decisions are implemented. Full PDF03 coverage is not asserted while Partial/Missing entries remain. No separate credit-note, OIDC, card-owner or FX manager-reapproval workflow is introduced.


Current implementation coverage: 42 Done, 13 Partial, 4 Missing, 0 Broken. Requirement groups are not a substitute for production acceptance.
