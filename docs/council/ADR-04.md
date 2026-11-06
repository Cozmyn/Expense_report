# PDF04 — architecture and owner scope

1. Preserve one workbook-compatible posting engine. String A:X cells are the serialization contract; typed trace and reconciliation are generated with those same cells. Serialization never recalculates VAT, splits a row or balances by rewriting credit.
2. Default vehicle percentage is50 for every vehicle mapping/payment. Finance writes dated versions into existing financial policy masters. Effective date plus version chooses the rule. Policies participate in accepted calculation/context/configuration hashes. Non50 codes are explicit, not renamed50 codes.
3. Source calculations preserve printed bases and VAT. Arithmetic still checks the document and gross. Monetary fingerprints use canonical decimal representations; UI uses existing exact-money transport/BigInt. Excel precision has a fail-closed boundary.
4. Preview→Finance review→immutable batch uses one fingerprint. Completing Finance review accepts that candidate. Export revalidates financial/configuration context inside Serializable transaction. After export, preview reads original stored rows and traces.
5. Normalized immutable posting lines reference original expense, calculation, verified VAT component, FX decision and mapping. A unique source-summary marker supports exact SQL aggregates without multiplying gross by the number of posting rows.
6. A business retry reuses the batch independently of the browser request key. Existing explicit void/external-reversal/reissue remains; no automatic ERP call or credit-note lifecycle is added.
7. Finance alone downloads accounting XLSX. Auditors retain scoped evidence/PDF and read-only preview, not Excel. Secure authenticated streaming and download audit remain.
8. D04-02 supersedes the proposed ERP tax-model selector/full-ledger balancing exercise. Raw source-like A:X is not falsely presented as a complete balanced ledger. D04-03 supersedes separate per-card car treatment. All earlier owner overrides remain.

No independent human Accounting approval or actual ERP import is claimed by generated fixtures. See API04 for additive schema9, user guide for RO/EN flows, and PDF04 verification for exact test evidence.
