# Antet decont / Report header

User direction: select payment/card once; name, department and LantID automatic; add receipts individually. Romanian default and English.

Implemented: five report fields: EmployeeName, EmployeeWorker (LantID), DepartmentCode, Payment and CardId. Identity snapshots come from the authenticated user's server account and active effective organization assignment when creating a report. Missing department is shown explicitly, never fabricated. Existing reports retain empty snapshot values rather than being assigned current employee data retroactively.

Header PATCH reuses expected report version, idempotency, role/state permissions and atomic audit. Payment/card are selected before receipt entry in the UI. Selection cannot contradict active receipts. Receipt controls inherit header payment/card; Apply rejects conflicting input; preflight checks parity. Legacy API reports with no configured header retain earlier line-based compatibility until header is configured. No automatic splitting or deletion. Worker accounting rule remains employee-based.

Schema upgrade 4 adds nullable payment/card and empty-default identity columns on existing installations, supports SQLite/PostgreSQL. Snapshot and PDF include header. Header selection and identity do not yet complete company scoping, travel fields, the full 39-column traceability or all PDF requirements.

Tests: four new backend cases, including a repeatable legacy-schema upgrade check, cover authenticated identity/department, one-time card selection and conflicting writes, and protection of existing receipt payment data. Desktop/mobile browser journey checks identity and absence of repeated payment fields. Functional commit 3799eaa064d5bbe0b566d469ae3677787cbb6bf0 passed full CI 41923056222 (both databases, desktop/mobile, container). Wording-only commit 8ebc83f9da55bea4a96b4ea07b19d6500426b6d9 passed frontend/backend/container; browser run 41923309585 is still installing dependencies at publication. No passing result is claimed for that pending job.

Owner check: create a report, review automatic details, select Personal or Company card, save header, add two receipts. Verify payment/card cannot be changed per receipt; attempt an API mismatch and expect report_payment. For different payment details create another report. Missing profile department must be corrected by the administrator for future reports.
