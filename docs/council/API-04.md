# PDF04 — API, data and migration

Owner contract: [D04 decisions](PDF04-OWNER-DECISIONS.md). RO default; EN available.

## Financial contract

`Posting.Build` returns the exact 24 A:X cells alongside typed source-linked lines and per-expense source→export reconciliation. It does not add ERP-generated tax entries or assert that raw A:X debit/credit sums constitute the complete ERP ledger. Verified source bases and paid VAT are retained; reconstruction is a check, not a silent replacement. Existing source-like car allocations remain at default50. Non50 requires configured corresponding tax codes; missing codes block, preserving original amounts.

- `GET/POST /api/finance/vehicle-deductibility`: Finance only. POST `{percentage, from, reason, taxCodes?}`, percentage0..100 with at most2 decimals. Default50 if no dated version. Same-day corrections create new versions; latest applicable date/version wins. Optional keys include `deductible21Code`, `nonDeductible21Code`, and11 equivalents. Future policy does not choose a different percentage for earlier document dates.
- `GET /api/reports/{id}/erp-preview`: Finance/Auditor read-only. Returns headers, unchanged rows, typed lines, reconciliation, version/fingerprint and issues. A saved active batch returns frozen original values despite later master changes.
- `POST /api/finance/reports/{id}/posting-review`: Finance; `{version,fingerprint,action:"ACCEPT"|"REJECT",reason}`. A stale fingerprint rejects409. Rejection returns to Finance review and retains manager revision. Completing Finance review also records acceptance of the exact generated candidate; no additional mandatory approver.
- `POST /api/finance/reports/{id}/export`: existing version request. Ready state/current manager and Finance approvals/current accepted financial snapshots/current preview acceptance required. Equivalent retry with a new HTTP key returns the previous batch for its original/current report version. Different inputs/versions conflict; void/reissue is explicit.
- `GET /api/finance/batches/{id}/lines`: Finance; immutable trace, verified VAT component/FX/calculation references, source dimensions and stored reconciliation.
- `GET /api/finance/posting-dashboard?from=&to=&state=&page=`: Finance,25 batches/page, maximum366-day range. Active-batch department/vendor/category aggregates count each source expense once using SQL minor-unit sums; void/reversed batches are excluded. Existing Finance worklist provides detailed source exception handling.
- `GET /api/batches/{id}/xlsx` and `GET /api/reports/{id}/accounting-details`: Finance only, including when a guessed URL is entered. Auditor source/PDF routes retain scoped access.

Mutation routes retain CSRF, request idempotency, object scope, reason/version checks and Serializable transactions. XLSX has one Import worksheet, fixed headers, numeric debit/credit and text cells without formulas/macros. Values that cannot survive Excel numeric precision are rejected rather than rounded silently.

## Schema release9

Run the normal init command (`--init`) before upgraded server startup. Additive upgrades create `PostingLines` and `PostingReviews`; extend `Batches` with pre-export report version, Finance hash, business key, posting fingerprint, reconciliation and captured report header. Legacy files/hashes/rows remain untouched; legacy traces are explicitly unavailable rather than reconstructed from current masters.

Posting lines have batch/expense/calculation/FX/VAT-component/mapping foreign keys, unique sequence and stable ID per batch, nonnegative one-sided debit/credit minor-unit constraints and one summary row per source expense. EF and database triggers protect posting lines/reviews and batch payloads. Batch lifecycle State/Reference/PostedAt remains writable only through existing authorised commands. Business keys are unique; corrections use explicit cycles. Source and posting fingerprints canonicalise decimal representations so database scale changes cannot create false stale decisions.
