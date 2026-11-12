# Wave 1 UX safety and accessibility acceptance checklist

Scope: UI only; no changes to server-side validations, accounting formulas, OCR extraction, role permissions, or original evidence retention.

## Must remain functional

- Employee: create report; choose payment/card/cost centre; upload one or more photos/PDFs; review OCR; manual entry; tax forms; save draft/receipt; submit, correct and resubmit.
- Manager: role-checked approval/return/reject, receipt/source comparison, comments, delegation where authorized.
- Finance: vendor resolution, VAT and FX calculation, card statement import, partial reconciliations, report verification, tax/currency policies, export generation/download, recorded ERP posting, void/reopen as authorized.
- Admin/Auditor: access management, routing, nomenclatures, read-only audit trails, job recovery and policy approval rules.
- All roles: localization RO/EN, error states, session expiration, keyboard access, direct document evidence.

## Wave 1 regression checks

1. **Supplier isolation.** Fill ERP account and reason for vendor A, open B, verify B is empty. Resolve B, verify POST uses only B ID and values; return to A, verify no cross-object values.
2. **Export isolation.** With two export batches, enter reference for A. B must remain blank. Confirm A; verify the external ERP reference and correct batch ID. Void/reopen must require a local reason, and must never claim the ERP reversal happened automatically.
3. **Finance settings isolation.** Open report > Calcul și FX. Inspect all per-receipt calculation, approval, history, and issue actions. Global VAT and FX editing must only appear in Configurare Finance. Verify all policy version/approval/acceptance actions remain there.
4. **Session recovery.** Open a report, expire session, reauthenticate as the **same** account. The report must reopen from the server, without trusting stale local report data. Reauthenticate as a **different** account: no report or work context from the previous user may be shown. Existing encrypted draft restore must stay opt-in.
5. **Multi-file upload.** Selecting one PDF should keep the existing immediate upload and OCR review. Selecting five photos each containing one receipt should show five independently configurable items; uploading should assign Single per photo without falsely marking multiple receipts. Set one item to Multiple and verify the server receives that specific mode.
6. **Partial upload failure.** Let items 1 and 2 complete and item 3 fail. Retry queue must retain only unfinished items and never automatically duplicate successfully uploaded originals.
7. **Document integrity.** Original attachment links and OCR provenance remain accessible. Manual entry must not destroy original documents, and every existing required verification must still block inappropriate submission.
8. **Narrow viewport**: 320px, 390px, 768px. Check login, sidebar, upload queue, receipt editor with VAT, Finance supplier/batch actions, and Finance settings. The primary document/page must not overflow horizontally; large accounting tables may scroll within their own container.
9. **Keyboard**: Tab, Shift+Tab, Enter, Escape. Closed mobile navigation must not be focusable, open menu must receive focus, Escape must close, focus must return to the trigger. Disabled actions must retain nearby reason text.
10. **Accessibility**: inspect actual computed contrast for normal text (at least 4.5:1 except applicable WCAG exceptions), visible focus indicators, 24px WCAG 2.2 AA target-size criteria with exceptions, 44px comfort target for touch; screen-reader labels and state announcements.
11. **Zoom/mobile**: 200% and 400% zoom, on-screen keyboard open, reduced motion enabled. Save/Cancel/Submit must remain reachable without accidental background interaction.
12. **End-to-end roles**: run existing Playwright suites and user journeys against an isolated test database only. Never point destructive OCR/Finance test flows at production.

## Acceptance gate

- `npm test --prefix ui`
- `npm run build --prefix ui`
- `npm run test:e2e --prefix ui` in an isolated UAT environment with seeded, non-production users and synthetic documents.
- Cross-check backend .NET tests and verify branch checks.
- Run manual keyboard and mobile assistive-technology passes. Automated checks are not WCAG certification.

This document is a test plan, **not** evidence that all checks have passed.
