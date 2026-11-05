# PDF 02 — implementation handoff

The former slice-only status is superseded by BLUEPRINT-02-CURRENT-MATRIX.md, API-02.md, ADR-02.md, TEST-02.md, SECURITY-02.md and VERIFICATION-02.json. The entire eight-page second PDF is mapped, including its two distinct ten-page fixtures. No workbook review was restarted.

Implemented code covers signed ingestion, scanning, text-layer/OCR extraction, source geometry, segmentation and manual alternatives, persisted group drafts and suggestions, source-linked human verification, immutable correction/raw history, complete-page preflight, retry/outbox/job safety and operational visibility. The recorded browser walkthrough adds a cover page to the ten-page/nine-expense layout and tests correction/verification/retry end to end.

Explicit approved departures: Finance retains ingestion in review; 5% and 19% are disabled; no OIDC/card-owner module; report identity and card/payment are selected once. Conceptual schema/API naming is adapted to the existing architecture through the ADR and API mapping.

Business-approved OCR thresholds/corpus/ERP acceptance and target deployment privacy/security/load evidence remain human release gates. The coding agent cannot sign them. See current immutable test/run references before assessing verification.
