# Blueprint 02 — current evidence / dovezi actuale

Functional code: dd5fbb81de0e00ce62a4fb44d40b8fba120f435a
Passing CI: https://github.com/Cozmyn/Expense_report/actions/runs/41931829184

| Requirement | Implementation evidence | Acceptance boundary |
|---|---|---|
| Finance may ingest/re-extract | Existing Reports.Edit permission/state gates retained; new Finance integration test | Confirmed user decision; employee cannot edit Finance review |
| 5% and 19% disabled | Rules.VatIssues and Posting.Build guards; disabled-rate tests | Raw source kept; never remapped to 21% |
| Ten pages -> eight or nine groups | DocumentSegmentationTests fixtures; consecutive page numbering | Both layouts preserved; general recall not certified |
| Two receipts on one page | Typed non-overlapping regions, manual add/crop, synthetic fixture | Automatic spatial quality still needs representative corpus |
| Immutable raw extraction and retries | DocumentReview + ExtractionAttempt, EF/database triggers, retry tests | Raw versions retained; reviewed data not overwritten |
| Split/merge/assign/support/delete/restore | OcrReviews versioned APIs + OcrGroupBoard | Original files/tombstones retained, reasons required |
| Original-to-expense lineage | DocumentGroup.ExpenseId and source regions; one-time verification | Missing invoice number rolls back expense creation |
| All pages accounted | Explicit unassigned/supporting ledger + submit preflight + Capture flag | Page count is not a claim of complete receipt detection |
| Original/verified financial values | Original group fields, CorrectedExpense and immutable audit | Structured correction-history access is a follow-up |
| Field geometry/confidence | Poppler text boxes, Tesseract TSV words, DocumentGeometry tests | Text-layer confidence is deliberately null; OCR score is recognition only |
| Preview/navigation/highlights/crop | Authorized hash-checked page route, rate limiting, DocumentViewer | No public object URL; new raster rendering is on demand |
| Partial errors and manual fallback | Failed-page records, manual page-review start, retry preservation | Some malformed/encrypted originals must be replaced |
| Scope/version/security | Cross-report denial, stale version, source integrity and immutable evidence tests | Full penetration/load/privacy deployment acceptance remains open |

Original source matrix is retained. Blueprint 02 is not fully accepted: durable event outbox, provider diagnostics, complete category suggestion/default contracts, representative quality corpus/thresholds, operational/performance evidence and recorded walkthrough remain open. No requirement is silently removed.
