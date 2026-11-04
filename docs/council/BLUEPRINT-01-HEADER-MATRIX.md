# Antet — trasabilitate / Header traceability

Supplement to the original matrix; no source IDs removed. Scope: Blueprint 01 pages 1–3 and 7–8, plus user's one-time header clarification.

| ID | Requirement | Files | Evidence/status |
|---|---|---|---|
| BP-H01 | Employee name automatic, read-only | Reports.Create, Domain, main | New server identity test; desktop/mobile assertions passed |
| BP-H02 | LantID automatic from Worker profile | Reports.Create, Domain, main | Same authenticated identity test passed |
| BP-H03 | Department from active effective assignment | Reports.Create, main | DEPT-1 snapshot test passed; missing assignment displayed explicitly |
| BP-H04 | Payment/card selected once per report | ConfigureHeader, Api, main, ReceiptModal | Server card selection/mismatch tests passed; receipt controls inherited |
| BP-H05 | Header versioning, permissions and audit | Api PATCH/Atomic, Reports.Edit/Version | Existing scoped mutation gates reused; identity not client supplied |
| BP-H06 | Preserve older databases | SchemaUpgrades release 4 | Legacy-column upgrade and repeated-upgrade test passed |
| BP-H07 | Header in immutable snapshot and PDF | Reports.Snapshot, ReportPdf | Code updated; existing end-to-end PDF generation passed; detailed print-layout acceptance remains open |
| BP-H08 | Receipt-by-receipt entry | main, ReceiptModal | Three-receipt desktop/mobile journey passed |

Functional acceptance: CI 41923056222, commit 3799eaa064d5bbe0b566d469ae3677787cbb6bf0, all jobs success. Final save-button wording rerun 41923309585 has successful frontend/backend/container checks and pending browser dependency installation. Full PDF implementation remains partial: company scoping, travel context, complete 39-column traceability, OCR and other recorded gaps remain open.
