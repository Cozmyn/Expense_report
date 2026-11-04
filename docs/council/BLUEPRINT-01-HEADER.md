# Antet decont / Report header

User direction: select payment/card once; name, department and LantID automatic; add receipts individually. Romanian default and English.

Implemented: six report fields EmployeeName, EmployeeWorker (LantID), DepartmentCode, Payment, CardId (five fields; name/worker/department plus payment/card). Identity snapshots come from the authenticated user's server account and active effective organization assignment when creating a report. Missing department is shown explicitly, never fabricated. Existing reports retain empty snapshot values rather than being assigned current employee data retroactively.

Header PATCH reuses expected report version, idempotency, role/state permissions and atomic audit. Payment/card are selected before receipt entry in the UI. Selection cannot contradict active receipts. Receipt controls inherit header payment/card; Apply rejects conflicting input; preflight checks parity. Legacy API reports with no configured header retain earlier line-based compatibility until header is configured. No automatic splitting or deletion. Worker accounting rule remains employee-based.

Schema upgrade 4 adds nullable payment/card and empty-default identity columns on existing installations, supports SQLite/PostgreSQL. Snapshot and PDF include header. Header selection and identity do not yet complete company scoping, travel fields, the full 39-column traceability or all PDF requirements.

Tests: three new backend cases cover authenticated identity/department, one-time card selection and conflicting writes, and protection of existing receipt payment data. Desktop/mobile browser journey checks identity and absence of repeated payment fields. CI pending.

Owner check: create a report, review automatic details, select Personal or Company card, save header, add two receipts. Verify payment/card cannot be changed per receipt; attempt an API mismatch and expect report_payment. For different payment details create another report. Missing profile department must be corrected by the administrator for future reports.
