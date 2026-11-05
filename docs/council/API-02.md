# PDF 02 API contracts

All routes below are under `/api`, require an authenticated active session, and resolve the report through server-side authorization. Mutations require CSRF, an Idempotency-Key and the expected report Version. Financial review mutations require a reason. JSON property names use camelCase.

| Operation | Route | Result |
|---|---|---|
| Authorize bounded intake | POST /reports/{report}/document-bundles | BundleRequest(version,name,size,mime,hash,reason); 15-minute actor/report/file-bound grant; no original object key |
| Transfer bytes | POST /reports/{report}/document-bundles/{document}/content | Multipart version/reason/file, X-Upload-Token; sniff actual signature and compare expected SHA/length/name/MIME; retained in AwaitingCompletion |
| Recover interrupted completion | POST /reports/{report}/document-bundles/{document}/authorization | Recheck scope/edit/version; authorise retained bytes; no file reselection needed |
| Complete upload | POST /reports/{report}/document-bundles/{document}/complete | BundleComplete(version,token,reason); 202; one ScanReport job; retries with same operation key replay |
| Pipeline status | GET /reports/{report}/document-status | Per-document scan/stage/page/error/group/attempt counts |
| Groups and field evidence | GET /reports/{report}/documents/{document}/review | available, page ledger, proposals, regions, fields, tax, alternatives, defaults, category suggestions, tombstones and duplicate warning |
| Initialize manual recovery | POST /reports/{report}/documents/{document}/review/start | VersionRequest; actual source hash/page count retained when OCR fails |
| Split, merge, region assignment, add second crop, supporting, delete, restore | POST /reports/{report}/documents/{document}/groups | GroupCommand(version,action,groups,regions,pages,reason); transaction and tombstones |
| Supporting page/reopen | POST /reports/{report}/documents/{document}/pages | PageCommand(version,page,supporting,reason) |
| Shared draft corrections | PATCH /reports/{report}/documents/{document}/groups/{group}/fields | OcrDraftCommand(version,data,reason); no verification/expense/export; raw provider result unchanged |
| Verify and create one expense | POST /reports/{report}/lines | LineEdit(version,data,reason,sourceAttachmentId,sourceGroupId); all domain checks and unique source link committed together |
| Correct verified expense | PATCH /reports/{report}/lines/{expense} | Revalidate linked source; exact before/after correction audit and printed-rate snapshots |
| Retry extraction | POST /reports/{report}/documents/{document}/extract | VersionRequest; scanned originals only; pending-job guard; reviewed data survives |
| Preview authorization | GET /reports/{report}/documents/{document}/preview-info | Page count + 5-minute viewer grant; active session still mandatory |
| Raster page | GET /reports/{report}/documents/{document}/pages/{page}/preview?viewer={grant} | Recheck actor/report/document/expiry and original hash, private/no-store raster; legacy cookie-only callers still recheck scope |
| Original | GET /report-attachments/{document} | Scope, definitive clean verdict and original hash verified |
| Raw extraction history | GET /reports/{report}/documents/{document}/extractions | Append-only version/fingerprint/raw evidence; authorised archived originals retained |
| Correction history | GET /reports/{report}/documents/{document}/feedback | Original, previous and verified values with reviewer/time/page/box |
| Durable lifecycle events | GET /reports/{report}/documents/{document}/events | Metadata only, delivery state/attempts; no raw receipt text in event payload |
| Employee allocation defaults | GET /reports/{report}/expense-defaults?date=YYYY-MM-DD | Effective, validated worker > department > company defaults |
| Operations | GET /reports/{report}/jobs; GET /admin/health; GET /admin/ocr-metrics | Scoped progress, finite retries, trained-model health/version and workload |

The PDF's conceptual /document-bundles and /document-groups paths map to report-nested routes to reuse established authorization and concurrency. Original multipart receipt/shared-file routes remain authenticated compatibility paths with the same signature/limits/scanning gates. They do not bypass Capture/group verification. Browser bulk capture uses the signed protocol.

Errors: 400 malformed/CSRF/idempotency; 401 missing/expired session; 403 role/edit/token denial; 404 inaccessible report/document; 409 stale version, consumed source, overlap or idempotency mismatch; 422 financial/metadata/type/size/coverage validation; 429 rate cap. RO/EN messages describe recovery. Background failure does not delete the upload.
