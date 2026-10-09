# Faza 1: audit inițial / Phase 1: initial audit

Date: 2026-10-09. Code baseline: `d23bc5639abbb1269d5f17c368673b945a457df6` (code parent `13257f9c5f6a4814a25e10f671646a58bbf4d800`). This is an initial audit and source inventory, **not production acceptance or a completed implementation phase**.

## Consiliul / Council

1. Principal Product Designer — hierarchy, components, interaction states.
2. UX Researcher / Information Architect — role journeys, cognitive load, accessibility.
3. Senior Full-Stack Architect — React/.NET/SQL architecture, APIs, security, performance.
4. Front-Office Specialist — employee entry, evidence, correction and self-service.
5. Back-Office / Admin Specialist — Finance operations, roles, configuration and masters.
6. OCR / Document AI Engineer — extraction, confidence, geometry and review.
7. Bilingual Content Designer — Romanian default, English, terminology and microcopy.
8. QA & Test Automation Lead — acceptance evidence, regression and failure cases.
9. Compliance & Data Specialist — audit, integrity, retention and privacy.
10. Product Owner — scope, dependencies, traceability and acceptance.

These are review perspectives applied by the assistant, not independent human certifications.

## Surse și trasabilitate / Sources and traceability

Read the original v2 simple-auth specification, the Product/Process/UX brief and the companion requirements workbook (all 14 sheets). The macro workbook is a confirmation source, not a replacement for the agreed target rules. Source hashes and locators are in `source-manifest.json` and `source-ledger.json`.

The initial matrix contains **1,579 numbered entries**: source obligations, table rows, explicit database-field and role-permission entries, repeated cross-references, 37 premium-request additions and 10 earlier conversation clarifications. They are **not 1,579 independent features**. All extracted nonempty source blocks have a ledger classification; semantic completeness of compound requirements still needs individual review. Headings/context retained in the ledger are not silently discarded.

Initial status: **1,567 Partial, 5 Broken, 7 Missing, 0 individually accepted Done**. Repeated references account for repeated findings. The requested four-status vocabulary has no Unverified value: `Partial` with an explicit pending-verification assessment is provisional, not proof of partial implementation. A passing suite cannot mark every related requirement Done.

See [complete matrix](INITIAL-MATRIX.md), [machine-readable matrix](initial-matrix.json), and [manifest](source-manifest.json). Every row includes source, fix and acceptance test; generic tests must be refined into field-level cases before acceptance. Preserve numbered IDs when updating.

## Constatări prioritizate / Prioritized findings

| ID | Severity | Finding and evidence | Required correction / acceptance |
|---|---|---|---|
| AUD-01 | Critical release gate | ERP equivalence to company-approved imports remains unproven (D-036/OI-005). Existing synthetic golden tests cannot supply company acceptance. | Keep exact 24 columns and `Import`; validate anonymized successful files when available. Continue development with explicitly provisional fixtures. |
| AUD-02 | High | `server/Posting.cs` selects the vehicle branch using raw `Taxes.Count`; `Rules.VatCodeCount` tolerance logic is used by reporting. BR-011/TBR-013 are not consistently connected to export. | Test 0% base at 0, ±1.99, ±2, ±2.01 RON with 11/21%, vehicle and FX; change branch selection without losing gross/base amounts. |
| AUD-03 | High | Receipt client VAT arithmetic uses transaction-currency tolerance; server validation converts to RON. | Share policy semantics and test client/server parity at FX boundaries. |
| AUD-04 | High, investigation | Posting project assignment reads a nonempty project without the same N/A policy guard used for cost center. Hidden stale values may reach ERP. | Reproduce policy change after draft entry; prevent N/A dimensions reaching final export while preserving revision history. |
| AUD-05 | High | `server/DocumentExtraction.cs` implements actual asynchronous OCR, but confidence defaults to null and extracted fields have no bounding boxes. | Extract page geometry and characterized confidence; document classification; low-confidence review; real-document accuracy validation. Never invent confidence scores. |
| AUD-06 | High | `ui/src/CapturePanel.tsx` has no field-to-document highlight or dedicated zoom/rotate/page controls; desktop panel orientation differs from the latest requested original-left layout. | Unified preview, safe derivative rotation, page navigation and keyboard-accessible field highlighting. Preserve original file hashes. |
| AUD-07 | High | OCR correction is represented by reviewed line/audit data rather than an explicit candidate-to-field feedback record suitable for evaluation. | Persist original suggestion, correction, source page, extractor version and reviewer; separate feedback collection from any claim of model training. |
| AUD-08 | High | Audit screens/export are capped (1,000/50,000), render raw codes/JSON, and do not cover the requested source/correlation context. | Paged readable diffs and complete authorized export; privacy-minimized request context; verify database append-only controls. Do not claim tamper-proof against database owners. |
| AUD-09 | High | Retention timestamp is set at posting; closure does not extend the five-year anchor and no verified lifecycle is implemented. | Resolve post/close anchor conservatively; legal hold must override deletion. No automatic purge until approved retention policy and restore tests. |
| AUD-10 | High | Financial master/version reproducibility and all role/object-level combinations have not been individually accepted. JSON-backed facts differ from the recommended normalized schema. | Trace each proposed field/constraint to actual storage; document equivalent implementation or migrate gaps; replay old revisions after master changes. |
| AUD-11 | High | Production ASVS, WCAG 2.2 AA, P95 load, encryption configuration and backup/restore objectives lack complete acceptance evidence. | Separate code controls from infrastructure controls; measured security/accessibility/load/restore gates before launch. No claim of an observed exploit from missing evidence alone. |
| AUD-12 | Medium | Finance dashboard counters navigate to the same general queue rather than distinct filtered tasks. Finance screen duplicates the general report list and worklist. | One queue, correctly scoped deep links, preserved filters and clear next action. |
| AUD-13 | Medium | Finance queue loads broad report sets and validates individual reports; no server paging, column control or bulk workflow. | Paged indexed queries, bounded validation work, role-scoped bulk actions, measured large-dataset tests. |
| AUD-14 | Medium | No dedicated My Profile page exposes normal self-service password change; no dedicated approval-history experience. | Implement SHR-02 and a searchable approval history without granting additional permissions. |
| AUD-15 | Medium | Long receipt modal lacks requested guided mobile steps; project helper text can say optional when policy requires it. | Short progressive sections, authoritative requiredness, focused inline errors and recoverable autosave. |
| AUD-16 | Medium | Mixed inline translations/raw role and event codes, hardcoded Romanian money formatting and browser-default dates. | Complete RO/EN catalog, bilingual glossary, explicit locale formatting, visible persistent language control across auth states. |
| AUD-17 | Medium | Master screens lack complete search/history/merge workflows; bank statement batch lineage is not represented by the recommended batch entity. | Trace import provenance, collision-safe transactional merges and historical references; admin acceptance cases for every master. |
| AUD-18 | Low | Bundler logs third-party `use client` directive warnings; no build failure. | Track dependency/tooling cleanup after business defects. Do not confuse warning removal with functional readiness. |

No newly demonstrated critical security exploit is asserted. Critical denotes a release gate, not an invented vulnerability.

## Evaluare UX: front office / Front-office heuristics

| Heuristic | Initial evidence | Target |
|---|---|---|
| Visibility of system status | Workflow and OCR statuses exist, but raw codes and inconsistent recovery text remain. | Plain-language next steps, save/error/retry states and background-job progress. |
| Match to real work | Multiple receipts, receipt/shared attachments and supplier branches exist; the long editor increases effort. | Receipt-focused guided entry: document → supplier/category → amounts/VAT → review. |
| User control and recovery | Draft/correction lifecycle exists; not all focus/return and inline error paths verified. | Safe leave, restore focus, resumable entry and clear corrections without retyping. |
| Consistency/error prevention | Dynamic field rules exist; optional labels and currency tolerance conflict with server rules. | Same requiredness, terminology and validation on every surface. |
| Recognition/accessibility | Labels are implemented but full keyboard, screen-reader, zoom and contrast audit is outstanding. | WCAG 2.2 AA verification across desktop/mobile, not checklist-only acceptance. |

## Evaluare UX: back office / Back-office heuristics

| Heuristic | Initial evidence | Target |
|---|---|---|
| Visibility/prioritization | Dashboard counts do not consistently open matching queues. | Exception-first queues with aging, exact filters and meaningful empty states. |
| Efficiency | Saved filters exist; duplicated lists, no complete column/pagination/bulk tooling. | Single Finance workbench with persistent context and safe bulk actions. |
| Recognition | Audit codes/JSON and broad master forms require technical interpretation. | Human-readable changes, searchable masters and precise accounting labels. |
| Error prevention | Preflight and audited actions exist; export tolerance gap remains. | Blocking consistent preflight, recoverable vendor/card issues and explicit destructive confirmation. |
| User control | Revision/export history exists; dedicated manager history and complete audit export need work. | Scoped history, immutable financial records and explicitly authorized reopen/restore. |

This is a code-and-flow heuristic review. It is not a completed usability study or fresh visual/accessibility certification.

## Decizii și întrebări / Dispositions and questions

- Preserve section 5.1 historical-to-target dispositions and section 5.2 rules together. Do not reintroduce old Excel corruption, first-blank-row stopping, Outlook approval or card-owner-as-Worker behavior.
- Retain simple application username/password authentication, one Romanian legal entity, manual ERP XLSX import, no historical transaction migration and no mandatory second Accounting approval. SSO/MFA/direct ERP/e-Factura are not silently added as launch requirements.
- Original WCAG 2.1 guidance is strengthened to the latest requested WCAG 2.2 AA. Original optional OCR automation is now an explicit implementation requirement.
- Keep training examples isolated from production; onboarding sample data must never become production expenses.
- IBAN/phone validation applies only where a documented business field needs them; do not collect new personal data merely to satisfy a generic form checklist.
- Light mode remains the initial standard; dark mode is optional until contrast, preview and print behavior are designed and tested.
- API route or schema differences may be equivalent implementations, not omissions, but require explicit source-to-endpoint/field mappings and tests.
- Open inputs remain OI-001 hosting, OI-002 OCR provider acceptance, OI-003 SMTP/recipients, OI-004 bank formats, OI-005 company ERP samples, OI-006 sizing. Development can proceed with self-hosted OCR and configurable adapters; production acceptance cannot invent these inputs.
- Clarify retention anchor if closure follows posting: safest interim choice is preserve data and use the later event, subject to formal policy. Do not perform speculative deletion.
- No source requirement is intentionally dropped in this initial audit. Existing schema/route variations remain pending documented disposition.

## Schimbări și teste / Changes and tests

This phase adds the matrix, exact-source ledger, source hashes, reproducible extraction scripts and this audit. **No application component, endpoint or database migration changed in Phase 1.**

Fresh checks in this session: five frontend tests passed; production frontend build passed. Inventory validation passed: unique numbered IDs and exactly one linked matrix row for each ledger obligation. Verified 51 selected server/UI/test/documentation blobs against the remote tree; only local `docs/STATUS.md` was stale and is excluded from this change.

Historical CI run `37783990281` on code parent `13257f9c5f6a4814a25e10f671646a58bbf4d800` succeeded: backend suite on SQLite and PostgreSQL (63 cases each), frontend tests/build, two desktop/mobile Playwright scenarios using real OCR/scanning, and container smoke checks. These tests do not prove every requirement or every role's UI. .NET is unavailable locally; backend and E2E suites were not freshly rerun in this phase.

## Criterii de continuare / Next-phase gates

1. Refine compound rows and pending statuses into field/action/role/API/export evidence. Preserve IDs and record changes after each phase.
2. Specify one design system and role flows against this inventory, including error recovery; do not remove financial fields to simplify layout.
3. Repair accounting parity first, then OCR review/preview, operational screens, localization and administration in reviewable batches.
4. Add meaningful unit/integration/E2E cases for repaired behaviors; measure real OCR, accessibility and performance.
5. Final acceptance requires every original requirement accepted or an explicit justified disposition, deployment evidence, owner QA, updated RO/EN guides and actual runtime screenshots. The current matrix does **not** claim that gate is passed.
