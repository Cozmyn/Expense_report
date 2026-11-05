# PDF 02 — implementation handoff

The former slice-only status is superseded by BLUEPRINT-02-CURRENT-MATRIX.md, API-02.md, ADR-02.md, TEST-02.md, SECURITY-02.md and VERIFICATION-02.json. The entire eight-page second PDF is mapped, including its two distinct ten-page fixtures. No workbook review was restarted.

Implemented code covers signed ingestion, scanning, text-layer/OCR extraction, source geometry, segmentation and manual alternatives, persisted group drafts and suggestions, source-linked human verification, immutable correction/raw history, complete-page preflight, retry/outbox/job safety and operational visibility. The recorded browser walkthrough adds a cover page to the ten-page/nine-expense layout and tests correction/verification/retry end to end.

Explicit approved departures: Finance retains ingestion in review; 5% and 19% are disabled; no OIDC/card-owner module; report identity and card/payment are selected once. Conceptual schema/API naming is adapted to the existing architecture through the ADR and API mapping.

Business-approved OCR thresholds/corpus/ERP acceptance and target deployment privacy/security/load evidence remain human release gates. The coding agent cannot sign them. See current immutable test/run references before assessing verification.

## Before → after

| Before | After |
|---|---|
| File attached as general evidence | Source-linked groups/pages/regions and one verified expense per group |
| Heading-only text splitting | Consecutive invoice continuity, geometric column detection and manual alternatives |
| Whole-PDF render failure could stop all pages | Text-layer-first, per-page raster fallback and explicit partial failures |
| Automatic orientation could read a scan upside down | Bounded rotation recovery, retained attempts and original-coordinate highlights |
| Private receipt draft could reopen stale OCR | Audited shared draft, authoritative reopening and stale-cache protection |
| Fast grouping actions could use an old version | Controls stay disabled until the displayed group version catches up |
| No lifecycle delivery ledger | Immutable transactional outbox, leased jobs, attempt diagnostics and preserved retry history |
| Slice-only documentation | Complete PDF-02 matrix, typed API mapping, ADR, security record, RO/EN guide, corpus report and recorded demos |

Final verification: 149 tests pass on each database, 5 frontend tests/build, 4 desktop/mobile browser workflows and constrained container startup. See VERIFICATION-02.json for exact code/run/artifact IDs. Business and production approvals remain external, not inferred from test success.
