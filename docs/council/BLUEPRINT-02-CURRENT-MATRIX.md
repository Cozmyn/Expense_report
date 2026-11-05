# PDF 02 — Requirements Traceability Matrix / Matrice de trasabilitate

Source: attached Part 02/05, v2.0, 04.11.2026, all eight pages. Original workbook is confirmation evidence; no repeated workbook redesign.

Scope: completion of the second PDF implementation. Original platform-wide 1,579 IDs are retained unchanged. **Done means implemented with listed evidence, not business or production sign-off.** External acceptance items remain Partial and are not counted as approved. No source requirement was silently dropped.

| ID | Source | Requirement | Status | Implementation / fix | Verification |
|---|---|---|---|---|---|
| PDF02-01 | §1 | Replace OLE embedding with immutable uploaded originals and expense links | Done | ReceiptFiles, DocumentGroup, Reports.Snapshot | OcrReviewTests |
| PDF02-02 | §1, §6 | Country and document type remain separate controlled decisions; invoice number mandatory with company CUI | Done | ReceiptModal, Rules.Validate | MissingInvoiceNumberRollsBackVerificationAndLineCreation |
| PDF02-03 | §2 | Many unrelated receipts in one PDF become separate proposed drafts | Done | ReceiptParser, OcrReviews.Import | OcrCorpusTests; browser demo |
| PDF02-04 | §2, §6 | Consecutive multi-page invoices remain one proposed group; conflicting IDs require review | Done | DocumentSegmentation.Group | DocumentSegmentationTests |
| PDF02-05 | §2, §7 | Two receipts on one page have independent non-overlapping source rectangles, including side-by-side layouts | Done | Panels, RegionsFor; manual crop | DocumentSpatialTests; ten-page nine-group fixture |
| PDF02-06 | §2 | Unreadable, blank and irrelevant pages remain in the source ledger | Done | OcrPage, DocumentReview, PageDisposition | BlankAndCoverPagesRemainExplicitlyUnassigned |
| PDF02-07 | §2 | Rotated/low-contrast scans retain review and recovery; OCR uses automatic orientation | Done | Tesseract --psm 1 + osd; immutable original | Real rotated/low-contrast browser scenarios |
| PDF02-08 | §2 | Different languages, separators, CUI prefixes and ambiguous currency/date are preserved | Done | ReceiptParser normalization, OcrField.RawValue | OcrSafetyTests; ambiguity tests |
| PDF02-09 | §2 | Ten pages/eight expenses with invoice pages 3–5 | Done | Conservative page grouping | TenPageFixturesAccountForEveryPageAndOneThreePageInvoice(false,8) |
| PDF02-10 | §7 | Harder ten-page/nine-expense fixture, two receipts on page 7 | Done | Source regions and grouped invoice | TenPageFixturesAccountForEveryPageAndOneThreePageInvoice(true,9) |
| PDF02-11 | §3.1 | Upload requires current report edit rights and Finance reason; no OIDC/card-owner expansion | Done | Reports.Edit/Version; signed intake | DocumentIntakeTests; FinanceRetainsGroupingRights |
| PDF02-12 | §3.1 | Signed intake binds actor, report, document, checksum, length, MIME and expiry | Done | UploadGrant; Data Protection; idempotent intent/content/complete | DocumentIntakeTests |
| PDF02-13 | §3.2 | Signature/size checks, malware quarantine and finite transport/page limits precede OCR | Done | Files.Mime; ClamScanner; Worker scan gate | ReceiptTests; intake mismatch/limit tests; real ClamAV browser runs |
| PDF02-14 | §3.2 | Parsing is bounded, non-root, capability-dropped and uses private temporary files | Done | LocalDocumentExtractor.Run; compose/Docker constraints | Constrained container startup CI |
| PDF02-15 | §3.3 | Keep original SHA, bytes, size, MIME, uploader and source page count unchanged | Done | ReportAttachment; hash checks; immutable metadata guard | Integrity/metadata tests |
| PDF02-16 | §3.3 | Record derived page dimensions and PDF rotation, and render thumbnails on demand | Done | OcrPage Width/Height/Rotation; DocumentGeometry; DocumentPages | DocumentGeometryTests; browser preview |
| PDF02-17 | §3.4 | Segmentation produces rationale, regions, alternatives and explicit unassigned pages | Done | DocumentSegmentation; review API | Grouping/conflict/region tests |
| PDF02-18 | §3.5 | Each extraction field carries normalized value, raw evidence, source page/box, version and recognition confidence when available | Done | OcrField; OcrTax; ExtractionAttempt | Geometry tests; real-image OCR browser test |
| PDF02-19 | §3.6 | No invented current 19% or silently remapped tax; 5% and 19% unavailable | Done | Rules, Posting, master validation and UI options | DisabledRatesCannotBeOverriddenOrPosted; master tests |
| PDF02-20 | §3.7, A:F | Suggest categories from active master vocabulary; never force category selection | Done | DocumentAssistance.Categories; ReceiptModal choice buttons | DocumentAssistanceTests |
| PDF02-21 | §3.7 | Supplier CUI lookup stays controlled; unknown valid IDs require confirmation and mismatches are visible | Done | ReceiptFiles lookup; existing supplier workflow; modal mismatch notice | ReceiptTests; browser CUI branches |
| PDF02-22 | §3.7, A:F | Suggest description only from explicit itemization; narrative remains editable | Done | ReceiptParser description with source evidence | DescriptionIsOnlySuggestedFromExplicitPrintedItemization |
| PDF02-23 | §3.7, I:J | Default cost centre/project from effective employee/department/company masters, never OCR guesses | Done | DocumentAssistance.Defaults; expense-defaults API | DefaultsComeFromEmployeeThenDepartmentThenCompanyAndRejectInactiveDimensions |
| PDF02-24 | §3.7, M:P | Retain printed paid VAT independently per rate; unknown paid VAT is not zero | Done | OcrTax; VAT_UNCERTAIN / VAT_AMOUNT_NOT_PRINTED; server validation | GrossAndRateDoNotFabricatePrintedVat; corpus |
| PDF02-25 | §3.8 | Amount/VAT reconciliation, date, required invoice number, duplicate and completeness checks | Done | Rules; Reports.Validate; source overlap guards | VAT, duplicate and review tests |
| PDF02-26 | §3.9 | Side-by-side original and proposed fields, responsive on mobile | Done | OcrGroupBoard, ReceiptModal, DocumentViewer | Desktop/mobile browser workflows |
| PDF02-27 | §3.10 | Explicit human verification records editor/time, exact corrections and source version; cannot auto-submit/approve | Done | OcrReviews.Verify; immutable audits and report revision | VerificationCreatesOnlyOneExpenseAndRetryPreservesHumanCorrections |
| PDF02-28 | §4 | Durable bundle/page/region/group/field/job/correction/expense lineage with database constraints | Done | Existing originals + DocumentReview/Group/ExtractionAttempt + events; schema releases 5/6 | Migration, unique expense link and transaction tests |
| PDF02-29 | §4 | Raw extraction and original metadata cannot be changed by corrections or retries | Done | EF guards and database immutable triggers | Immutable extraction/event tests |
| PDF02-30 | §4 | One physical page may serve multiple independently verified regions | Done | Normalized SourceRegion list and overlap checks | DocumentSpatialTests; source-overlap tests |
| PDF02-31 | §4 | Deletion/supporting disposition needs a reason; originals and audit tombstones remain | Done | Group commands, source archive confirmation, historical access | GroupsAreScopedVersionedAndSupportSplitMergeAndDisposition |
| PDF02-32 | §4 | Retries are idempotent and do not create duplicate expenses or overwrite verified corrections | Done | Stable source keys, unique ExpenseId, lease + attempt concurrency | VerificationCreatesOnlyOneExpenseAndRetryPreservesHumanCorrections; browser retry |
| PDF02-33 | §4 | Report participants only may access source and derived documents | Done | Reports.Get/Scope on each document API | Cross-report and preview authorization tests |
| PDF02-34 | §5 | Provider-neutral extraction/progress/health/version contract, deterministic PDF text first and real OCR fallback | Done | IDocumentExtractor and LocalDocumentExtractor; ADR-02 | Geometry/parser/health tests; real Tesseract browser scans |
| PDF02-35 | §5 | Uncertain/outage states keep manual review; partial failures retain successful pages | Done | OcrPage.Error, manual review/start, retry | ManualReviewCanStartAfterOcrFailure; partial fixtures |
| PDF02-36 | §5 | Printed instructions never execute tools or send documents externally; bound extraction payloads | Done | Local CLI args use generated paths; no model/network executor | Prompt-injection golden case; security review |
| PDF02-37 | §6 | Zoom, rotation, page navigation and field source highlights without altering originals | Done | DocumentViewer; authenticated raster previews | DocumentGeometryTests; browser preview controls and demo |
| PDF02-38 | §6 | Split/merge/assign/add second crop/mark supporting/regroup/delete/restore with expected version | Done | OcrReviews commands; regroup re-extracts only values belonging to selected regions | Group tests; recorded split/merge walkthrough |
| PDF02-39 | §6 | Correct candidate fields in a shared, unverified, audited draft; retain private autosave too | Done | PATCH groups/{group}/fields; per-group drafts | SharedDraftCorrectionIsVersionedUnverifiedAndPreservesRawEvidence; browser demo |
| PDF02-40 | §6 | Original, previous and verified VAT/gross values remain inspectable | Done | feedback API; original extraction versions; immutable tax snapshots | Structured feedback tests; recorded VAT 21→2.10 correction |
| PDF02-41 | §6 | All pages assigned or explicitly supporting and all active groups verified before submit/Finance/export | Done | OcrReviews.Problems; Reports.Validate; Capture flag | CapturedBatchCannotBypassReview; browser 11-page/9-expense demo |
| PDF02-42 | §6 | Uncertain boundaries display alternatives and require human grouping confirmation | Done | Grouping evidence panel and verification checkbox | Conflict tests; split/merge demo |
| PDF02-43 | §7 | Typed upload intent/completion/status/groups/split/merge/correction/verify/retry APIs with idempotency and optimistic version checks | Done | See API-02.md; existing paths mapped explicitly | HTTP and browser tests |
| PDF02-44 | §7 | Asynchronous completion returns 202; users see stage/page progress and attempts | Done | DocumentIntake.complete; Worker.Progress; DocumentStatus | Signed upload completion test; browser polling |
| PDF02-45 | §7 | Short-lived signed preview grant plus active session/report checks; no object key as client authorization | Done | DocumentPreviewTokens; private/no-store; JsonIgnore storage keys | Preview-token and source integrity tests |
| PDF02-46 | §7 | Durable transactional outbox for upload/segment/extract/correct/verify/fail and idempotent notices | Done | DocumentEvents + dispatch Job saved with source transaction | DocumentEventTests rollback/immutability/idempotency |
| PDF02-47 | §7 | Finite attempts, leases, recovery, dead-letter state and safe admin retry | Done | Worker, Job StartedAt/CompletedAt/FailureCode; immutable stage events | Job concurrency/retry tests; operations UI |
| PDF02-48 | §8 | Synthetic tagged golden corpus and measured segmentation, gross/VAT/number precision/recall and page representation | Done | tests/Fixtures/ocr-corpus.json; OcrCorpusTests; CI JSON artifact | Automated corpus report |
| PDF02-49 | §8 | Required blank/orphan/multilingual/unknown VAT/19%/partial/retry/duplicate/invalid input coverage | Done | Corpus plus integration/geometry/intake/review suites | See TEST-02.md |
| PDF02-50 | §8 | Real raster OCR, PDF pipeline, rotated and low-contrast images exercised | Done | Playwright-generated synthetic inputs, ClamAV + Tesseract | Desktop/mobile platform workflows |
| PDF02-51 | §8 | Operational dashboard distinguishes queues, quarantine, review/correction workload and dependency health from accuracy | Done | ocr-metrics; admin/health; JobsPage; model availability/version probes | Health/outbox tests; dashboard smoke |
| PDF02-52 | §8 | Measure asynchronous 10-page usability and end-to-end demo time with progress/retries | Done | Recorded 11-page walkthrough + attached timing JSON | PDF02 recorded source-to-expense walkthrough |
| PDF02-53 | §9 | Runnable worker, migration, storage policy and failure/runbook documentation | Done | SchemaUpgrades, deploy/Dockerfile, ADR/API/operations docs | Database and constrained container CI |
| PDF02-54 | §9 | Romanian default + English labels/help and user guide | Done | Review components, master names, guide/glossary | Both viewport workflows; language/profile regression |
| PDF02-55 | §9 | Recorded source-to-expense demo, split/merge, cover, VAT history and retry stable IDs | Done | ui/e2e/ocr-demo.spec.ts; video/screenshot artifacts | Desktop and mobile demo CI |
| PDF02-56 | §8 | Finance-approved representative corpus, thresholds and Accounting/ERP sign-off | Partial | Acceptance register; cannot be signed by coding agent | Requires named human approval |
| PDF02-57 | §3.2, §5, §8 | Production encryption, IAM, regional retention, backups and deletion/legal-hold controls accepted on deployed infrastructure | Partial | S3 AES256 upload; development local storage only; deployment acceptance checklist | Requires target deployment evidence |
| PDF02-58 | §8 | Independent security/load/privacy acceptance against the target infrastructure | Partial | Automated security regressions plus SECURITY-02.md; no certification claim | Requires target environment and owners |

## Disposition decisions / Decizii

- Finance ingestion/re-extraction remains allowed during financial review by explicit user approval. This supersedes the narrower Draft/Returned example in §3.1. Existing role, state, reason and version checks apply.
- Both 5% and 19% are disabled in active master data, forms, domain validation and posting. Raw OCR labels remain preserved. The illustrated 5% workbook column does not authorise transactions at 5%.
- Authentication remains application-managed; enterprise OIDC and card-owner workflow were explicitly excluded.
- A deployment represents one internal company. A storage object key never grants access; report participants are checked on every request. A future multi-company service requires tenant isolation beyond this deployment.
- Conceptual SQL tables and REST paths are adapted to the existing typed EF/report architecture; mappings and reasons are in ADR-02.md and API-02.md.
- Text-layer boxes have no OCR recognition score. Rule-based boundary confidence remains unknown rather than a fabricated probability; rationale and alternatives remain visible.
- Originals are logically archived, not physically erased. Retention/legal-hold disposal is an explicit production-owner decision; no new destructive erasure was assumed.

Coverage: 58/58 identified requirement groups are mapped; 55 code/deliverable groups implemented, 3 external acceptance groups remain Partial. Approval of those three must come from real owners.

Current test results and immutable commit/run references are in VERIFICATION-02.json.
