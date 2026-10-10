# Implementare: loturile 1–2 / Implementation: batches 1–2

Council: Architecture, Accounting/Data, Front Office, Bilingual Content, QA and Product Ownership.

## Modificări / Changes

| Înainte / Before | Acum / After | Files |
|---|---|---|
| Vehicle branch uses raw VAT-row count | Uses tolerance-aware count with final RON conversion; retains small-zero base rows and one complete vendor credit | server/Posting.cs, Rules.cs |
| VAT policy absent from posting fingerprint | Effective VAT policy participates in posting version | server/Posting.cs |
| Client checks one transaction-currency unit | Debounced read-only server preview calls the same VAT arithmetic as submission; failure is visible and retryable | server/Api.cs, ui/src/ReceiptModal.tsx |
| Invalid zero amount may divide by zero | Structured validation issue returned | server/Rules.cs |
| Hidden N/A project can enter ERP | Validation and posting ignore non-applicable project dimensions; original input remains in history | server/Rules.cs, Posting.cs |
| Required project labelled optional | Label follows server field policy | ui/src/ReceiptModal.tsx |
| No normal self-service profile | Bilingual profile, read-only roles, language and password change | ui/src/ProfilePage.tsx, main.tsx |
| First-login password page lacks language switch | RO/EN available there too | ui/src/main.tsx |
| Inconsistent focus/touch foundations | Semantic palette, visible focus, larger default targets, reduced-motion handling | ui/src/style.css |

New endpoint: authenticated, CSRF-protected `POST /api/vat-validation`. Body: ExpenseData; result: `issues`, `expected`, `reconstructed`. No report, line, audit or business record is persisted by this read-only calculation. Missing FX or invalid amount/date returns a localized error. This is a preview, not authorization to submit or export; normal server gates still run atomically. No database migration.

## Teste / Tests

Added 18 backend cases: eight small-zero vehicle boundaries across 11/21%, three foreign-currency small-zero cases, two RON arithmetic tolerance boundaries, zero amount, N/A project, invalid applicable project, VAT policy fingerprint and authenticated/non-persisting preview. The last includes anonymous rejection and an accepted boundary. Existing suites remain intact.

Desktop/mobile Playwright scenarios now also cover profile navigation, RO/EN switch, password mismatch recovery, successful password change and revocation of another session. A profile screenshot is captured in the CI artifact. Existing employee/OCR/manager/Finance/export flows remain in the scenarios.

Local frontend tests: 5 passed. TypeScript/Vite production build: passed. Backend on the financial commit: 81 passed on SQLite; PostgreSQL job also successful. Final combined commit `6796130c003496fb75f3fd7e8d6bfc6ad380d238`: CI run 38025409719 succeeded, including both database suites, desktop/mobile browser scenarios and container validation. See VERIFICATION-CURRENT.json.

## Limite / Limits

This implements a subset of batches B1 and B2, not the entire Phase 2 design. Full financial configuration replay and company-approved ERP golden acceptance remain open. No new OCR confidence/geometry, guided full-page editor, paged Finance table, master merge, complete audit export or retention lifecycle is claimed. Palette/focus changes are not a WCAG certification. No production deployment occurred.

## Verificare de către proprietar / Owner QA

1. Open Profilul meu / My profile, switch RO/EN, confirm profile and roles cannot be edited.
2. Try mismatched new passwords, then a valid change; confirm another session is signed out.
3. Enter a domestic EUR receipt with an internal rate of 5: a 0.20 EUR VAT variance is 1 RON; 0.21 EUR exceeds tolerance. Confirm server preview and submission agree.
4. Set project N/A for the selected policy. Confirm a previously populated project is absent from ERP column Project; make it applicable and confirm validation returns.
5. Compare 11/21% vehicle exports with a 0% base of 1.99, 2.00 and 2.01 RON. Verify the zero-rate amount and total vendor base are preserved.
6. Confirm the exact 24 headers and Import worksheet remain unchanged. Use company-approved fixtures for acceptance before live accounting.

## Statusul cerințelor / Requirement status

Phase-3 matrix preserves all 1,579 IDs. Affected Broken/Missing items become Partial with concrete implementation evidence, not automatically Done. Source-level acceptance and original-document coverage are distinct from the number of passing tests.
