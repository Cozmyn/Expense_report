# Blueprint 01 — reconciliere / reconciliation

Source: 01_Report_Workflow_and_Product_Blueprint(2).pdf, v2.0, 04 November 2026, pages 1–8. Identical extracted text to copy (1). Workbook SHA-256 confirmed: a37f9fd3e8e09d8db93c40e780d3d0f4c7eb9e8b8c4286c375b8740a147f5038.

## Decizii confirmate / Confirmed decisions

User, 4 November 2026: retain username/password; do not implement enterprise OIDC. Preserve employee-based Worker; do not change export Worker to card owner. These override blueprint pages 2 and 8. The user decides further conflicts. No Accounting approval is implied.

## Conflicte rezolvate / Resolved conflicts

The user confirmed: 50 receipts maximum, 5% VAT disabled, separate reports by personal/company-card payment method. See BLUEPRINT-01-DECISIONS.md for enforcement and verification. These replace the earlier provisional mixed-payment behavior. No open decision remains among the conflicts raised so far.

## Implementare începută / Implementation started

BP01-004 (page 2, journey 4): clearing receipt gross/base/VAT fields preserves blank text in editor/autosaved UI draft rather than immediately coercing to zero. Blank VAT components block save; native required fields also apply. Numeric conversion occurs at API calculation/save boundaries, retaining the existing typed API contract. This does not complete versioned transactional draft storage or conflict recovery.

Files: ui/src/ReceiptModal.tsx; ui/e2e/platform.spec.ts. Desktop/mobile regression checks verify an emptied 0% base remains blank and blocks save until restored. CI pending. No schema/API/authentication/export change.

## Următorii pași / Next steps

Reconcile each header and all 39 columns with field ownership, source/derived values, API/storage, audit and test. Extend existing traceability without deleting any original IDs. Implement report header, draft concurrency, structured preflight, company authorization and complete acceptance walkthrough in vertical slices. Existing Phase 1/2 packages remain partial; neither source inventory nor this note proves acceptance.
