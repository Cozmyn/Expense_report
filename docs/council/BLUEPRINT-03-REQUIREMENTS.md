# PDF 03 — Requirements and initial disposition

Source: attached Part03/05 v2.0, 04.11.2026, all12pages. PDF SHA-256: b8cb81df5af0b739872ffed172d4cd6ef8f284e3740ccbf93425f4d08691b989.

Status applies to the new PDF03 target. **The new arithmetic core is not connected to live approval/posting. Main financial behavior is unchanged while the owner resolves policy decisions.** No user decision is silently replaced by a workbook example.

| ID | Source | Requirement | Status | Existing evidence / implementation / next fix |
|---|---|---|---|---|
| PDF03-001 | §1 K:L | Preserve verified original gross/currency, parsed values and corrections | Partial | OCR raw/correction history exists; per-expense accepted calculation ledger missing |
| PDF03-002 | §1 M:P; §2 | Printed VAT is input evidence; do not calculate printed VAT from gross | Done | InvoiceArithmetic + existing OCR raw evidence |
| PDF03-003 | §2, §6 Y:AB | Reconstruct positive-rate bases VAT/(bps/10000) in invoice currency | Partial | Tested policy-neutral InvoiceArithmetic; live posting still uses entered bases pending D01 |
| PDF03-004 | §2 Y13 | 0% VAT zero; zero base must be documented or explicitly approved residual | Partial | Core requires documented base; residual activation blocked pending D04 |
| PDF03-005 | §2 | Preserve exact decimal intermediates and declare rounding checkpoints | Partial | Core exact/rounded strings and explicit rounding/exponent; active policy pending D02 |
| PDF03-006 | §2 | No floating-point arithmetic for booked money; currency exponents | Partial | New core exact decimal string contract; frontend/live paths and currency-policy configuration still need integration |
| PDF03-007 | §2 | Negative credit notes use separate tested policy | Partial | Core blocks without explicit authority; production workflow pending D05 |
| PDF03-008 | §2 Q13 | Foreign/no-company-CUI treatment is explicit policy, not silent OK | Missing | Existing no-domestic-VAT branch retained; add applicability outcomes, authority and versions |
| PDF03-009 | §2 | Distinct unknown/not_present/explicit_zero/exempt/outside_scope states | Partial | Core typed evidence states; UI/database policy integration missing |
| PDF03-010 | §3 | Versioned company/country/document/effective-date VAT policy | Missing | Add draft/approval policy entity and migration; generic masters insufficient |
| PDF03-011 | §3 | Unique typed VAT component per expense/rate with source region and verification | Partial | Existing TaxData/OCR provenance; persistent financial component ledger missing |
| PDF03-012 | §3 | Immutable input/output hashes, precision, FX steps, policy decisions | Partial | Core deterministic hashes; accepted persisted ledger and histories missing |
| PDF03-013 | §3, §6 | Unsupported 19% never silently maps to21%; 5% disabled by user | Done | Existing hard validation/posting/master guards; core regression tests also reject 500/1900 bps |
| PDF03-014 | §4 X13 | Per-expense manual/bank FX candidates, rate/date/source/version/evidence | Missing | Existing dated FX masters and bank matches are not separate proposals/decisions |
| PDF03-015 | §4 | Accounting enters approved rate or converted amount with exact precision and reason | Missing | Add role-scoped proposal/decision flow and immutable history |
| PDF03-016 | §4 | Bank fingerprints, card/employee/merchant/date/amount matching; ambiguous alternatives | Partial | Existing imports/matching/allocations; proposal-only FX and explicit alternatives policy need integration |
| PDF03-017 | §4 | No automatic winner when bank and manual amounts differ | Missing | Current matched settlement becomes final; D03 required before replacing this behavior |
| PDF03-018 | §4 | Fees/markup/partial settlement/reversals get distinct policy treatment | Partial | Partial allocations exist; explicit fee/reversal decisions missing |
| PDF03-019 | §4 | FX unresolved blocks final export; locking and approval records | Partial | Existing missing-FX/card gates; per-expense decision lock missing |
| PDF03-020 | §4, §6 | Manager reapproval after RON change by approved policy | Partial | Current any-personal-increase reapproval; confirm target D03 |
| PDF03-021 | §4 E13 | Controlled supplier registry, unknown/review outcome | Done | Existing supplier lookup, confirmation and Finance registration |
| PDF03-022 | §4 R13 | Date-effective category-to-GL mapping | Done | Existing Posting.Find(accounting,category,date) |
| PDF03-023 | §4 S13; §5 | Local account maps from category rather than wrong Excel row references | Done | Existing category map; new regression covers positions13–99 |
| PDF03-024 | §4 T13 | Worker remains expense employee by earlier explicit user decision | Done | PDF card-holder instruction superseded; regression tests expense employee |
| PDF03-025 | §4 U13 | Date-effective category-to-subaccount mapping | Done | Existing accounting mapping; include stable IDs in new ledger later |
| PDF03-026 | §4 V13 | Cost-center ERP code is explicit master data, no LEFT slicing | Done | Existing costCenters.erpCode mapping |
| PDF03-027 | §4 W13 | ERP vendor account linked to verified CUI | Done | Existing supplier master/account-period validation |
| PDF03-028 | §4 Y:AB | Do not mix RON with invoice currency for base reconstruction | Partial | Core reconstructs in original currency; legacy TaxCurrency RON needs reviewed migration/integration |
| PDF03-029 | §4 AC:AG | Typed VAT set; do not inherit ±2 near-zero anomaly as rule | Partial | Typed core components; existing VatCodeCount tolerance remains pending D04 |
| PDF03-030 | §4 AH:AI | Versioned original-currency and RON reconciliation deltas/status | Partial | Original-currency core reasons/variance; persisted approval/FX/RON allocation ledger missing |
| PDF03-031 | §4 AJ:AL | Rate-specific tests and approved thresholds; 21% uses21, not19 | Partial | New current-rate/mismatch tests; active tolerance decision D02 outstanding |
| PDF03-032 | §4 AM13 | Sum versioned computed bases | Partial | Core component sum; financial snapshot/export integration missing |
| PDF03-033 | §5 S19:S99 | Record defects with source coordinates, reproductions, corrected rule and approval | Partial | Audit/formula trace and 87-position regression; accounting acceptance remains human |
| PDF03-034 | §5 X13:X99 | Replace hardcoded workbook rates across currencies | Partial | Existing dated master FX; per-expense approval flow missing |
| PDF03-035 | §5 I10 | Database sums for all valid expenses, no fixed Excel range | Done | Existing active-line sums; 50-receipt report cap preserved |
| PDF03-036 | §5 Sheet8 clear | Stateless deterministic export regeneration, no stale rows | Done | Existing values-only XLSX and immutable export batches |
| PDF03-037 | §5 Sheet3 clear | Confirmed reversible reset/archive; never destroy originals | Partial | Existing audited archives; dedicated reversible report-reset command not implemented |
| PDF03-038 | §5 Module1 | Validate all active records; no first-blank-country/active-sheet assumption | Done | Existing per-line validation with explicit country |
| PDF03-039 | §5 Module4 | Output includes all report expenses, no A1:Q50 cutoff | Done | Existing dynamic report PDF/export |
| PDF03-040 | §5 named ranges/master data | Discard #REF! and sample placeholders; production lists validated | Partial | Existing master imports; real production-data/privacy acceptance is external |
| PDF03-041 | §6.1 | Separate OCR_RAW/HUMAN_VERIFIED/ACCOUNTING_OVERRIDE money/source evidence | Partial | OCR and human history exist; accepted Accounting override layer requires implementation |
| PDF03-042 | §6.2 | Resolve policy by company/country/CUI/kind/category/date | Missing | Add deterministic policy resolver with overlap and gap handling |
| PDF03-043 | §6.4 | Compare rounded bases + preserved paid VAT against printed gross | Partial | Core implemented; live policy and exception integration pending |
| PDF03-044 | §6.5 | Convert only after original-currency calculation; preserve RON differences | Missing | Add approved FX calculator/rounding allocation and signed decisions |
| PDF03-045 | §6.6 | Stable mapping IDs/versions and reproducible payload shared by approval/export | Partial | Existing ERP version hashes; per-expense immutable calculation mapping missing |
| PDF03-046 | §6.7 | Accepted snapshot includes input/source/policy/mapping/FX/output/actor/version/time | Missing | Report snapshots are not a substitute for accepted expense calculations |
| PDF03-047 | §7 VAT001/002/004/005/006/008 | Current/mixed/zero/mismatch/foreign-currency mathematical fixtures | Done | InvoiceArithmeticTests, including original-currency and zero-evidence cases |
| PDF03-048 | §7 VAT003/007 | 5% sample pass superseded; 19% rejected | Done | Explicit user decision takes precedence; both tested as unsupported |
| PDF03-049 | §7 VAT009/010/011/012 | Unknown/no-CUI/foreign/credit applicability and authority | Partial | Unknown/credit math tested; applicability policies and workflow still missing |
| PDF03-050 | §7–8 policy register | Rounding/tolerance, deductibility, 0%, FX precedence/fees/reapproval need real sign-off | Missing | Open policy decisions D01–D05; existing vehicle rules retained rather than reinterpreted |
| PDF03-051 | §8–11 formulas E:AM | Maintain source-to-rule registry; do not execute Excel in production | Partial | Coordinate trace above and current code mapping; new finance ledger remains incomplete |
| PDF03-052 | §11 VerificaTVA | Do not turn workbook green/amber colors into approval; exact decimal recon | Partial | Core typed reason codes; named variance/override policy activation pending |
| PDF03-053 | §12 preview | Scoped provisional calculate-preview endpoint and bilingual financial UI | Missing | New core is a library only; existing vat-validation unchanged |
| PDF03-054 | §12 verify/history | Transactional accepted finance snapshot and readable version/source diffs | Missing | Add authorization, immutable entities, API and UI |
| PDF03-055 | §12 policy author/approve | Draft policy and controlled activation with separation of duties | Missing | Require separate author and approver; no auto-active Finance policy |
| PDF03-056 | §12 FX APIs/history | Manual/bank proposals, Accounting decision and histories | Missing | Implement typed proposal/decision services and UI after D03 |
| PDF03-057 | §12 invalidation | Changing inputs invalidates calculation/dependent export candidates | Partial | Existing report version/reapproval; new calculation lineage invalidation required |
| PDF03-058 | §12 exceptions | No is_valid=true bypass; named override policy + actor/reason | Missing | Existing reason-only VAT override needs replacement at new finalization boundaries |
| PDF03-059 | §12 acceptance | Golden/property tests, no float, provenance, Finance approval before PDF04 | Partial | Core/property and existing regressions tested; full integration and human acceptance remain open |

## Scope and execution plan

1. Audit source and current formulas/services; map every requirement — completed in this register.
2. Resolve financial-policy conflicts with owner; model draft/approval lifecycle — decisions pending.
3. Implement decimal reconstruction, immutable ledgers, FX decisions and invalidation — arithmetic foundation started.
4. Connect bilingual Finance/employee forms, previews, histories and export gates — not started.
5. Run database/property/integration/browser regressions and record real Finance acceptance — foundation regressions only.

Requirement groups mapped: 59. This is an initial matrix, not a completion claim.
