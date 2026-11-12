# Good-to-have UX – implementation and acceptance

Stacked on Wave 3 / PR #28; do not merge into main until all preceding waves are reviewed and accepted.

## Implemented (UI only)
1. **Employee drafts:** Most recently updated in-progress report remains the main Continue action; up to 3 additional drafts can be opened from the dashboard, and the full reports list remains available.
2. **Bank statement import:** Three visible stages: file/card → mapping/preview → confirm. Existing CSV/XLSX preview, duplicate detection, upload/import APIs and column-mapping preferences remain unchanged. Blocking errors are explicit.
3. **ERP posting preview:** A small summary of import rows, reconciled receipts and blocking exceptions; detailed 24-column Import table is preserved in an expandable section, with the same original values and export actions.
4. **Admin user search:** Search by name/login/email/code/cost-centre, filter by role and active state, show result count and reset. No role or user records are removed.
5. **Admin password reset:** Separate confirmation form scoped to the selected user, with separate reason and password fields; changing the account cannot inadvertently use the password-reset form state.
6. **Personal reporting filters:** The existing user-scoped `/preferences/{key}` backend persists grouping and query. Resetting the current view does not overwrite the saved preference.
7. **Audit journal:** Readable action labels with raw action code retained; before/after history accessible in an expandable details element; original backend audit rows and CSV export unchanged.
8. **Finance list density:** Compact/comfortable view toggle without hiding fields or changing backend filtering and role checks.

## Regression and usability checks
- Employee with 4+ editable reports: sorted by update timestamp; select second draft and return; no report is lost or duplicated.
- Finance: CSV 1/2/3 stage hints match available actions; duplicate and invalid rows still block import, and valid files use exact original payloads.
- Posting preview: compare the 24 technical columns against downloaded XLSX, verify VAT and debit/credit consistency rules unchanged; toggling details must not lose data or permission checks.
- Admin: search/filter all users, edit a specific user while filters remain applied, reset password of another user with explicit confirmation; inspect audit trail and failed authorization.
- Reporting: saved preferences are scoped to the signed-in account; different users cannot read each other's filters; resetting view only resets local display.
- Audit: action name fallback leaves unknown codes legible; raw action, actor and before/after JSON remain intact; filter/export remain consistent.
- Finance: compact mode must not hide a status, exception, amount or clickable report.
- Mobile: 320px/390px/768px, keyboard, zoom to 400%, focus indicators, screen reader; technical tables may scroll within containers.

## Release gates
- `npm test --prefix ui`, `npm run build --prefix ui`
- Existing .NET backend tests on SQLite and PostgreSQL
- Playwright Chromium desktop/mobile against isolated seeded users, not production
- Manual QA/assistive-technology review for Employee, Manager, Finance, Admin and Auditor

This is an acceptance plan, not proof that all test cases passed.
