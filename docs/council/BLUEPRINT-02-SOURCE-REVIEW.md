# Blueprint 02: verified source groups / grupuri sursă verificate

Confirmed user decisions: Finance retains ingestion and re-extraction in financial review. Rates 5% and 19% are disabled; original OCR text is preserved and never remapped to 21%. Authentication and report-header decisions remain unchanged.

Core slice implemented: document review ledger, source regions, append-only extraction attempts; explicit supporting-page dispositions; version-checked merge/split/add/region/disposition/deletion with audit tombstones; raw extraction history; original-to-expense lineage; human verification and duplicate source-region prevention; submission preflight for unassigned pages and unverified groups. Retry preserves existing reviewed groups and corrected expense data. Header payment/card remains authoritative. Finance uses existing role/state/reason controls.

Receipt creation with SourceGroupId verifies one group atomically. Repeated verification returns the existing report instead of creating another expense. Correcting a linked receipt revalidates and records corrected expense JSON alongside immutable extraction history. Archiving a linked receipt reopens the source group. Legacy source attachments can explicitly start review and link existing receipts. Existing manual/shared supporting-file workflow is retained.

Normalization now tries a PDF text layer before image OCR per page and records a failed page without discarding successful pages. Worker lease renewal and attempt concurrency protect long-running jobs from concurrent consumers. No external document provider was introduced.

Schema release 5: DocumentReviews, DocumentGroups, ExtractionAttempts. ADR: typed EF entities with serialized typed region/field collections, linked to existing immutable originals; audited mutations use report expected-version and existing idempotency transaction. No public object URL or storage key authorizes review access. Extraction attempts are append-only in EF and database triggers.

RO: Încarcă PDF-ul, verifică grupurile și paginile neatribuite, unește/separă după caz, marchează paginile justificative cu motiv, apoi verifică și salvează fiecare bon. Confirmă datele pe original. Modificările grupării necesită motiv. Nu poți trimite până când grupurile sunt verificate și toate paginile sunt atribuite sau justificate.
EN: Upload the PDF, review groups and unassigned pages, merge/split as needed, mark supporting pages with a reason, then verify and save each receipt. Check all values against the original. Group changes require a reason. Submission remains blocked until groups are verified and all pages are assigned or supporting.

Tests added: scope/version checks, merge/split/tombstones/page disposition, one expense per verified group, retry preserving corrections, Finance permissions, missing invoice rollback, immutable extraction history, region overlap and disabled rates. Existing browser journey now explicitly confirms original verification. CI pending.

Not complete stage acceptance: automatic multipage continuity and spatial segmentation, real field confidence/bounding boxes, interactive crop/field highlighting, deterministic multi-page previews, durable outbox, complete corpus/metrics/load/security/privacy acceptance and demo recording still require implementation. The review ledger is not a claim of OCR accuracy or complete implementation of the PDF.
