# PDF03 audit and policy decisions / audit și decizii

## Experts/perspectives

Finance domain, financial architecture, data integrity, bilingual UX and QA. These are review perspectives; no real accountant approval or separate human council is claimed.

## Conflicts already resolved by explicit user instructions

- PDF pp3/6/7 illustrate 5% as active and VAT003 says pass. The user disabled 5%, including historical input. Keep 5% rejected, preserve raw source labels and record this fixture as intentionally superseded.
- 19% remains rejected in every date context, never silently remapped to21%.
- PDF p4 T13 replacement mentions card holder. The user explicitly chose Worker=expense employee and excluded a card-owner workflow. Keep employee Worker; regression covers every original row13–99 with a different card owner.
- Keep max50 active receipts, separate personal/card reports, report-header identity/payment, and existing application-managed authentication.

## Prioritized gaps

**Critical:** bank settlement currently becomes final after Finance matching, without a separate immutable FX decision/policy selection; accepted per-expense calculation ledger is absent; reason-only VAT override is not a named approved exception policy; live posting consumes entered bases rather than a canonical derived-base snapshot.

**High:** original-currency-only VAT reconstruction differs from existing optional RON VAT entry; current arithmetic tolerance1RON and gross0.01 differ from the PDF's unapproved per-currency thresholds; near-zero0% suppression is still a default2RON branch-placement rule; policy activation lacks separate author/approver; currency exponents and frontend exact-money contract are not fully integrated; credit/fee/reversal policies are undecided.

**Medium:** financial-policy history/diff and explicit non-domestic applicability outcomes need UI/API/data integration; report reset is not a dedicated reversible command; production corpus/configuration sign-off remains external.

## Original proposals (historical; superseded)

D01 — **Base authority.** Target PDF says derive positive-rate base from printed VAT/rate, retain printed base as comparison evidence. Existing export uses the entered base. Proposed: derived base is the calculation result, conflicting printed base needs a named Finance exception; never overwrite the original.

D02 — **Rounding/tolerances.** Existing app rounds half-away-from-zero to2 decimals, allows1RON VAT arithmetic difference and0.01 gross difference. Proposed: half-up at configured currency exponent; accept at most1minor unit for gross and printed-base comparison (0.01 for RON/EUR); larger differences block until a named approved exception. No tolerance is approved by this audit.

D03 — **FX and reapproval.** Proposed: bank/manual candidates remain proposals; Finance explicitly approves final rate or RON amount and resolves differences. Preserve existing reapproval on any increase in personal reimbursement unless the owner supplies a materiality threshold.

D04 — **Zero-rate evidence.** Proposed: only documented0%-base, no inferred residual; count every documented component including bases below2RON. This replaces legacy near-zero suppression. Foreign/no-company-CUI flow stays simple and non-reclaiming for Romanian VAT, but gains an explicit policy outcome/reason/version rather than silent OK.

D05 — **Credits.** Proposed: keep negative documents blocked until a separately approved credit-note/refund/reversal policy defines workflow and accounting. Mathematical sign behavior is tested independently and does not enable production credits.

Fuel/car deductibility is not reinterpreted as legislation. Existing branches remain in place; new policy versions must carry actual Accounting authorization before acceptance. Fees/reversals/unexplained differences remain unresolved review cases, not automatic divisions or invented0% amounts.

## Concrete work done

`server/InvoiceArithmetic.cs`: policy-neutral decimal string contract, current-rate guards, exact and rounded original-currency bases, documented zero-base handling, typed evidence states/reasons, precision/sign validation, explicit rounding/tolerance inputs, stable input/policy/output hashes. It cannot mark an expense ready for booking, has no activation API and changes no live financial path.

`tests/InvoiceArithmeticTests.cs`: golden current/mixed/zero/mismatch/foreign-currency examples, missing/unknown values, disabled5/19, sign-aware credits behind authority, rounding strategies, input-order fingerprints, bounded generated sign/total invariants and rejection of float/scientific/locale-ambiguous input.

`tests/BusinessTests.cs`: regression that all original positions13–99 map the same category to the same GL/local account and employee Worker, independent of another card owner.

The owner resolved D01–D05 on 05.11.2026. See FINANCE-DECISIONS-2026-11-05.md for the current rules and implementation. The proposals below are historical and do not govern runtime behavior. Broader PDF03 gaps remain explicit in the matrix.

## Implementation update — 05.11.2026

Live arithmetic, Finance currency settings, explicit per-expense FX decisions, immutable accepted calculations, source/policy invalidation, bilingual Finance workspace and export consumption are implemented. Manager reapproval on FX-only changes has been removed by owner instruction. Signed documents use the normal flow with warnings. ≤2 RON rounding uses a separate accounting adjustment, without guessed 0% base or additional attachment. Free-text VAT override is rejected. Broader policy lifecycle/acceptance gaps remain visible rather than being marked complete.
