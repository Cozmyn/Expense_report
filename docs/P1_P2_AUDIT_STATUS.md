# Audit 11 November 2026 — P1/P2 implementation and acceptance

This implementation is staged in **PR #23** on top of **PR #21** (both Draft until review and CI signoff). Production launch gates DB-01, OPS-01/02 and OPS-03 live in the separate PR #22 and still require actual infrastructure rehearsal.

## Traceability

| Audit ID | Code / test | Acceptance and remaining restrictions |
| --- | --- | --- |
| API-01 — Safe retries | PR21 `ui/src/api.ts`, PR23 cryptographically hashed request identity + tab-scoped 24-hour key retention | Same actor, path, body, method reuse the committed response; no request payload is written to browser storage. Check network timeout, refresh, and replay; 24-hour limit does **not** prove arbitrary historic duplicate prevention |
| OCR-04 — Date and total semantics | PR21 `DocumentExtraction.cs`, semantic parser tests | Issue date preferred; due date, advance, change and RON equivalent not blindly substituted; ambiguity remains a visible human task |
| OCR-07 — Omitted receipts | `OcrReviews.cs`, `OcrGroupBoard.tsx`, `ReceiptCoverageTests.cs` | Explicit per-page count of visible receipts (including 0 on supporting pages), checked against verified groups and bound to a group-state signature; split/merge invalidates old attestation. **This is human verification, not computer-vision proof of completeness** |
| DB-03 — Overlapping validity | `Api.Atomic` serializable SQL transactions, 409 retryable conflicts, `FinancialCalculations.cs` date gate; `IntervalConcurrencyTests.cs` | Concurrent HTTP writes reject conflicting active periods (test on PostgreSQL). Privileged direct SQL writers bypass application-level validation; enforce DDL privileges/DB restrictions in Production |
| UX-03 — Field states | `receiptProvenance.mjs`, `ReceiptModal.tsx`, tests | Unread, OCR-proposed, calculated, manually changed and human-verified states; count and links for fields awaiting attention. Financial approval stays server-side |
| UX-06 — Safe drafts | PR21 optimistic `draftRevision`, PR23 close flush, retry and compare local/server snapshots | Two tabs receive 409 rather than silent overwrite. Loss of network/auth retains open form and shows failure. **Closing/crashing the browser with offline unsaved changes is not an encrypted persistent local draft feature**; keep UAT gate open for that scenario |
| LRN-01 — Rare suppliers | PR21 streams verified groups and caps **per detected supplier** | No global last-300 cutoff. A high-volume production database still benefits from a normalized indexed supplier identity |
| LRN-05 — Multiple templates | `SupplierOcrTemplates.cs`, `SupplierOcrLearning.cs`, `SupplierTemplates.tsx`; template tests | Each vendor can have distinct layout IDs with append-only versions; at least two independent human-selected/verified originals required. Disabling one layout creates an inactive revision while preserving its examples; disabled supplier layouts cannot leak back through implicit history |
| FE-01/02 — Accessibility | PR21 `DocumentViewer.tsx`, `ReceiptModal.tsx` | OCR region percent inputs and confirmation via keyboard; focus trap for modal, reduced trapping in inline workspace. Real keyboard/screen-reader testing still required |
| API-03 — Multi-instance jobs | `Storage.cs`, `JobRecoveryTests.cs`, `JobRecoveryIntegrationTests.cs` | Expired OCR/scan/outbox lease is reclaimable via atomic conditional update; unknown SMTP delivery is quarantined instead of auto-retransmitted. Two PostgreSQL workers compete for only one lease. Live worker SIGKILL, rolling deploy and manual SMTP reconciliation still need testing |
| API-02 / OPS-05 — Resource isolation | PR21 stream-to-temp ZIP and source-size ceiling; PR23 `Documents.cs`, `DocumentPages.cs`, `ReportPdf.cs`, `BoundedExportTests.cs` | PDF generation bounded at 2 per instance, 16 MiB response and 120 pages; preview rendering throttled and bounded. Current PDFsharp produces bytes in RAM and S3 `IStore.Get` still buffers one original at a time; concurrency/capacity test in UAT |
| UX-05 — Merchant | PR21 persists supplier display name, PR23 registry lookup no longer silently replaces printed name | Fiscal CUI / official legal name is kept distinct from the original merchant/trading name; official-name replacement is an explicit user action |
| UX-02 — Stable workspace | `CapturePanel.tsx`, `ReceiptModal.tsx`, `main.tsx`, `style.css` | No auto-open after OCR; persistent receipts index on left, source/form side by side, editable inline panel, Save and next; mobile single column |

## Real-document pilot (100–200 receipts)

`deploy/ocr_pilot_metrics.py` accepts only a locally curated JSONL file with **100–200 annotated real receipts** and an adjudicated ground truth. The parser does not upload or OCR documents; it computes aggregate correctness for amount, date, currency, supplier and VAT; missed/joined/split receipts; median and nearest-rank P90 human review durations, manual corrections, supplier-frequency cohorts and template usage.

Each entry needs an anonymous unique `id`, `source: "real"`, a `scenario` code in `T01`–`T22`, `ground_truth` and `ocr` field maps for `amount`, `date`, `currency`, `supplier`, `vat`, numeric `verification_seconds` and comparable `manual_seconds`, integer `corrections`, `omitted_receipts`, `wrongly_merged`, `wrongly_split`, `supplier_frequency: "rare"|"frequent"` and boolean `template_used`.

The evaluator fails closed if any of the 22 scenario IDs are absent or the cohort size/ground truth is invalid. Its output contains **only aggregates**, never the document contents or supplier names. The audit's negotiable **≥30% reduction of median verification time** is a *target*, not achieved performance. Labelled synthetic fixtures used in CI are **only tests of the evaluator** and do not count as a real pilot.

```sh
python3 -m unittest discover -s deploy -p 'test_ocr_pilot_metrics.py' -v
python3 deploy/ocr_pilot_metrics.py \
  --jsonl /secure/uat/redacted-receipt-pilot.jsonl \
  --report /secure/evidence/ocr-pilot-summary.json
```

## Remaining acceptance and release blockers

1. CI must pass on the stacked PR combination (#21 + #23), including PostgreSQL/SQLite backend tests, frontend build/tests and real browser workflows; keep PR23 Draft until review.
2. Run tests with two authenticated browser tabs, session expiry, network loss, keyboard-only/OCR selection, screen reader, print previews and restart. Verify no silent overwrite and that manual retry preserves the same idempotency ID.
3. Inject worker SIGKILL during scan/OCR/SMTP, restart on another instance and check no duplicate financial or notification effects; investigate an unknown SMTP outcome with message ID before manual resend.
4. Load test large PDF/ZIP exports and concurrent 2+ web replicas; watch memory, CPU, storage and response latency. Current limits mitigate pressure; they are not fully streaming end-to-end.
5. Complete the real-document pilot and operator-reviewed field/segmentation discrepancies.
6. Separate P0 Production configuration, PITR restore and signoff in PR22 must be closed before release.

**Review policy:** no auto-merge, no Production signoff solely from CI. Any required database schema changes for high-volume learning should follow the P0 versioned migration mechanism; none are introduced here.
