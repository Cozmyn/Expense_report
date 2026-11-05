# ADR 03 — Controlled financial configuration and reproducible calculations

Status: implemented; real production Accounting/ERP/corpus acceptance remains external.

## Decisions

1. Preserve owner D01–D05: original-currency VAT bases, Finance-only FX/rounding, preserved approval estimate and no FX manager reapproval, no additional evidence for ≤2RON rounding, signed normal flow. 5/19 remain disabled, Worker stays the expense employee, max50 active receipts and separate personal/card reports, no OIDC/card-owner feature.
2. Separate operational currency settings from tax-policy governance. Currency/FX adjustments remain direct Finance actions. Tax policy is an immutable authored draft activated by a different Finance user. Scope, dates, priority and specificity are deterministic; no silent ambiguous winner or gap fallback.
3. Keep source and calculations separate. Accepted versions pin original input, source hashes/regions, full mapping data and account versions, policy authority/approval, currency settings, FX proposals/decisions, exact intermediates, reasons, actor/time and output hash. Normalized components are unique per accepted version/rate. Export rejects a receipt pointing to another receipt's calculation, even if values and shared attachment are identical.
4. Classify bank principal, fees and markup explicitly before allocation. All transactions contributing to a bank FX confirmation require explicit classification; no-charge cases use NORMAL. A statement attached to a receipt can be linked to charge expenses without duplicating bytes or claiming it contains all receipts. All parts reconcile exactly to the original statement RON amount. Charges are distinct RON expenses with clean statement coverage; principal alone enters FX allocation. Corrections append revocations and archive generated expenses only after match undo. Negative/reversal values retain the same workflow.
5. Reset is an archive operation with an immutable restore record; no original deletion. State/version/ownership/confirmation/empty-target checks prevent destructive merges or alteration of posted exports.
6. Negotiate exact monetary strings rather than break legacy numeric API clients. Browser totals, OCR aggregation, comparison, draft submission, bank reconciliation and rate JSON serialization use decimal strings/BigInt. A lossless parser handles historical numeric JSON while preserving counts. Currency exponents and half-away/half-even rounding are versioned Finance settings.
7. Preserve vehicle category accounting for exempt treatments while emitting no invented VAT code. Keep deductibility in approved reference mappings; do not infer legislation or add a magic percentage editor.
8. Real Finance configuration acceptance is an explicit, authenticated, version-hashed action. Production exports block until that acceptance exists. The software/test suite never signs for an accountant or claims real-corpus OCR accuracy.

## Persistence and migration

Schema release8 adds TaxPolicies/TaxPolicyDecisions, VerifiedVatComponents, ReportResets/ReportRestores, BankTreatments/BankTreatmentRevocations and FinanceAcceptanceRecords. Existing line JSON accepts optional treatment/evidence fields; existing originals, report snapshots, posted exports and source hashes remain untouched. Financial calculation engine context is `financial-calculation/2`, so stale earlier confirmations require Finance verification. `--init` is transactional/idempotent on SQLite and PostgreSQL. Imported bank rows become immutable source records and retain original currency precision.

## Validation

Unit/integration/property coverage plus desktop/phone workflows exercise real scan/OCR and correction, author/approver roles, policy ambiguity/gaps/retirement, special treatment, normalized provenance and stale mapping detection, exact JSON/decimal operations, source preservation on reset/restore, bank fee splitting/undo, calculation attribution, production acceptance and unchanged FX approval revision. Container gates verify reproducible constrained startup. Final execution links are in the completion verification document.

## Consequences

Source references and historical decisions remain inspectable. Configuration changes can deliberately block unposted exports pending re-verification. Tax drafts do not affect active calculations or acceptance. Real master data, bank-charge categories/accounts, policy references, representative documents and ERP acceptance still require responsible Finance users.
