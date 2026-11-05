# Decizii Finance / Finance decisions — 05.11.2026

These owner decisions replace the provisional D01–D05 proposals. They are product authorization, not a claim of external tax/legal sign-off.

| ID | Decizie / Decision | Implementation | Verification |
|---|---|---|---|
| D01 | Baza se reconstruiește din TVA tipărit/cotă și se verifică totalul. / Reconstruct base from printed VAT/rate and reconcile gross. | InvoiceArithmetic, FinancialCalculations.Preview, accepted PostingJson consumed by Posting | InvoiceArithmeticTests; FinancialWorkflowTests |
| D02 | Numai Finance administrează cursul și rotunjirea. / Finance alone administers FX and rounding. | Finance-only immutable currency settings, controlled precision/method/tolerance; manual rate or exact RON proposal and explicit decision | Finance-only authorization, precision, immutable policy tests |
| D03 | Aprobarea utilizează estimarea disponibilă la completare; schimbarea FX nu solicită reaprobare. / Approval uses the entry-time estimate; FX changes do not request manager reapproval. | Latest effective entered estimate captured in EstimatedRon/FxVersion; bank match is a candidate; explicit Finance choice; no FX revision/notice; source changes evaluated at original approval rates | FinanceFxAdjustmentKeepsManagerRevisionAndApprovedEstimate; original source-increase regression |
| D04 | Baza 0% are document justificativ. Rotunjirile de cel mult 2 RON nu solicită document suplimentar. / Zero-rate base needs source evidence; rounding up to RON 2 needs no additional document. | No guessed 0% residual, no near-zero suppression; normal clean receipt evidence required; separate rounding amount/account; maximum configurable tolerance 2 RON | Signed ±2 and ±2.01 boundaries; source/scan checks; export mapping guard |
| D05 | Sumele negative utilizează același flux, cu avertizări. / Negative amounts use the same flow with warnings. | Signed entry, VAT sign checks, bank import/allocation; negative ledger amounts reverse debit/credit sides without a second workflow | Signed arithmetic/property tests; signed bank import; posting tests |

## Flux RO

1. Angajatul completează antetul o dată, apoi bonurile. TVA și baza sunt în moneda originală. Valorile OCR rămân dovezi, nu devin aprobare automată.
2. La completare, suma RON estimată folosește cel mai recent curs Finance valabil disponibil pentru monedă. O versiune nouă de curs nu rescrie estimările anterioare.
3. Managerul aprobă estimarea capturată. Un extras bancar asociat nu schimbă singur valoarea aprobată.
4. În „Calcul și FX”, Finance alege estimarea, suma bancară reconciliată sau o ajustare manuală. Propunerea, decizia, actorul și motivul sunt păstrate. Nu se redeschide aprobarea managerului.
5. Finance compară baza introdusă cu baza calculată și confirmă baza/totalul pe original. Politica de rotunjire este versionată. O diferență de cel mult 2 RON nu creează bază 0% și nu cere un nou atașament.
6. Finalizarea Finance generează calcule imuabile. Exportul verifică intrările, sursele, politica și mapările curente; o schimbare invalidează calculul. ERP folosește baza confirmată; fișierul contabil detaliat păstrează și baza originală.

Pentru diferențe peste toleranță, corectați datele față de original. Un motiv liber nu permite ignorarea erorilor. Nu este activată o politică de ajustare fiscală nelimitată.

## Flow EN

Enter header once and receipts individually. VAT-derived bases are calculated in original currency. Entry-time FX estimates are preserved for manager approval. Finance explicitly chooses estimate, reconciled bank total or manual rate/RON amount, with immutable decisions and no manager reapproval. Finance verifies base and gross against scanned source evidence. Rounding up to RON 2 is separate from 0% base and needs no additional attachment. Finalization persists accepted calculations; export rejects stale inputs/policies/mappings and uses confirmed bases. Original entered bases remain in detailed accounting evidence.

## Configuration and migration

Run the existing `--init` deployment step before starting the upgraded application. Schema release 7 adds FinancePolicies, FxProposals, FxDecisions, Calculations and nullable line references/amounts. Append-only records have EF guards and database triggers. Existing posted batches are unchanged. Unposted historical records with converted RON VAT must be verified in original currency; no silent conversion migration is performed.

Finance currency settings: effective date, currency amount decimals (0–6), rate decimals (0–12), half-away-from-zero/half-to-even, tolerance (0–2 RON), optional estimated rate. Generic FX master creation/import/retirement also requires Finance. Currency settings are operational owner-authorized settings; they do not represent a multi-company tax-policy approval module.

ERP settings require real `roundingAccount` and `roundingLocal` when a rounding row is needed. These must be supplied by Accounting. No account number is invented.

## API

- GET/POST `/api/finance/currency-policies` — Finance only, append-only versions.
- GET `/api/reports/{id}/financials` — report-scoped previews, proposals, decisions, accepted histories.
- POST `/api/reports/{id}/lines/{line}/calculate-preview` — scoped provisional calculation.
- POST `/api/finance/reports/{id}/lines/{line}/fx-proposals` — MANUAL (rate or RON), ESTIMATE, BANK_STATEMENT (full reconciled allocation).
- POST `.../fx-decisions` — explicit Finance selection, optimistic report version, source fingerprint check.
- POST `.../verify-financials` — Finance verification and immutable calculation.

Mutations use existing CSRF, idempotency keys, optimistic versions and transaction handling. FX changes cannot alter exported/posted reports through these endpoints.

## Owner QA

- Employee/Admin cannot save Finance currency settings or generic FX changes.
- Enter 100 EUR with configured 5 RON estimate; manager sees 500 RON. Finance applies 5.123456: final is 512.35, estimate remains 500, manager revision unchanged.
- Match a bank transaction: bank amount appears separately; Finance must explicitly choose final FX before completing review.
- Enter mixed 0/11/21% VAT; compare entered and computed bases. Try 5/19%, mismatched signs and missing source: confirmation/export must block.
- Verify +2/-2 RON rounding warns; +2.01/-2.01 blocks under the default policy. No extra attachment for the accepted ≤2 RON difference.
- Enter a negative receipt and negative statement transaction; verify warnings and normal approval/reconciliation/export flow.
- Change relevant policy/source after confirmation: export must require reconfirmation.
- Download accounting details: original amounts and Finance calculation references appear separately. ERP remains exact 24-column values-only schema.

## Remaining broader PDF03 scope

Do not read this delivery as 100% completion of the entire PDF. The traceability register retains partial/missing items for the separate multi-dimensional tax-policy author/approver module, exempt/outside-scope intake, dedicated fee/markup classification, full field-level source diffs, reversible whole-report reset, and actual Finance/ERP acceptance on production documents. Those are not replaced with fabricated policy approval or test data.
