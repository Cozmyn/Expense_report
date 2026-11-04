# Blueprint 02 — audit and first safeguards / audit și primele protecții

Source: 02_Batch_PDF_OCR_and_Expense_Capture(1).pdf, v2.0, pages 1–8. Workbook SHA-256 matches a37f9fd3e8e09d8db93c40e780d3d0f4c7eb9e8b8c4286c375b8740a147f5038.

## Decisions requiring user clarification

1. PDF section 3.1 allows intake only on Draft/Returned reports. Existing platform allows Finance to upload/extract during financial review and employees to edit rejected reports. Preserve existing permissions until user decides which states/roles can ingest or re-extract.
2. PDF sections 3/8 list unsupported 19% OCR output. Existing effective-date master policy can allow rates other than 5%. User has explicitly disabled 5%, but has not categorically disabled historical 19%. Preserve financial policy; ask whether 19% always blocks or can be reviewed for eligible historical dates. OCR must never remap 19 to 21.

Confirmed overrides remain: no OIDC; Worker is employee; 50 receipts maximum; 5% disabled; separate personal/company-card reports; payment/card selected in report header. Eight- and nine-expense ten-page examples have different layouts and are separate fixtures, not a contradictory total. REST naming variants can be reconciled without changing semantics.

## Priority findings

Critical: missing versioned document groups/regions, page-disposition ledger, verified-source lineage, immutable original extraction/correction history and retry-to-expense uniqueness.
High: no multipage continuity grouping, geometry/confidence/highlights, split/merge/region editor, verified-group gate, per-page failure recovery, complete durable outbox/event lifecycle.
Medium: signed intake/preview contracts, text-layer-first extraction, operational metrics/corpus/recorded demo and complete provider contracts remain partial.

Existing genuine local OCR, MIME checking, malware scanning, private originals, asynchronous jobs, human receipt editing and source attachment links are retained. Their existence does not satisfy all new requirements.

## Implemented first batch (not stage completion)

- Repeatable candidate IDs for identical page/segment text. This alone does not guarantee expense-creation idempotency; a durable verified-group uniqueness contract remains required.
- Explicit unassigned page list, including blank and cover pages. Cover text alone no longer becomes a fake receipt. UI warns that processing is incomplete and originals remain available. This is not yet a full page-disposition/submit gate.
- A rate/gross line without an explicit tax label cannot invent printed VAT. A missing printed value remains absent and gets VAT_AMOUNT_NOT_PRINTED review guidance. Existing explicit printed-VAT base reconstruction remains a suggestion, not proof of printed base.
- All 12 workbook currencies recognized; localized nonbreaking and narrow-space number grouping supported.
- 19 new parser test cases. No migration or role/rate-policy change in this batch. CI pending.

## Traceability register

| ID | PDF section | Requirement | Status and next work |
|---|---|---|---|
| BP02-01 | 1 | Separate country/CUI and invoice number | Existing partial; retain domain checks |
| BP02-02 | 2 | Many documents and multipage invoices | Partial: heading-based proposals; implement grouped continuity |
| BP02-03 | 2, 6 | Two receipts on one page | Partial: text segmentation; spatial regions missing |
| BP02-04 | 3.1 | Intake authorization | Clarification pending |
| BP02-05 | 3.1–3 | Secure upload, quarantine, immutable originals/renders | Partial; signed intake and page metadata missing |
| BP02-06 | 3.4 | Segmentation confidence/rationale/unassigned pages | Partial: unassigned list added; no calibrated boundaries |
| BP02-07 | 3.5–6 | Field raw/normalized values, boxes/confidence, alternatives | Partial: text evidence exists; complete model missing |
| BP02-08 | 3.7–10 | Proposed drafts, checks, review and verified lineage | Partial; durable verified-group workflow missing |
| BP02-09 | 4 | Bundle/page/group/region/field/job/duplicate entities | Missing complete schema; retain originals/jobs during migration |
| BP02-10 | 4,7 | Retry idempotency, immutable corrections and outbox | Partial: stable proposal IDs added; durable uniqueness/history missing |
| BP02-11 | 5 | Provider abstraction, text layer, local fallback, privacy | Partial: local extractor exists; no third-party transmission introduced |
| BP02-12 | 5 | Missing printed VAT must not become guessed VAT | Safeguard added; complete review exceptions still open |
| BP02-13 | 6 | Review workspace, source highlights, split/merge/assign | Missing complete workspace |
| BP02-14 | 6 | Correct/verify/retry/delete tombstones, group history | Partial existing receipt editor; group history missing |
| BP02-15 | 6 | Failed/quarantined/partial states and per-page recovery | Partial jobs; per-page model missing |
| BP02-16 | 7 | Typed bundle/group/page APIs, versioning, auth | Missing new complete contracts |
| BP02-17 | 8 | Golden corpus, segmentation/field/page metrics | Missing representative accepted corpus; new safety tests are not corpus acceptance |
| BP02-18 | 8 | Security/privacy/load acceptance | Partial existing tests; complete stage acceptance open |
| BP02-19 | 9 | Workers/storage/ADRs/observability/demo | Partial existing operations; complete package open |
| BP02-20 | 6–8 (pages) | Preserve raw values; 5/19% rate handling | 5 disabled per user; 19 clarification pending |

No original requirement is dropped. This supplementary register does not replace the existing source matrix or claim 100% implementation. Next full vertical slice: versioned source groups, explicit page dispositions and transactional verification-to-expense creation, then spatial review and failure recovery.
