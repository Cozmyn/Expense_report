# API 03 — Finance controls

All mutations retain session authentication, CSRF, idempotency keys, transactions and report optimistic versions where report data changes. Finance-only actions never grant manager permissions.

| Method/path under `/api` | Permission | Contract |
|---|---|---|
| GET `/finance/tax-policies` | Finance | Drafts plus immutable approve/retire decisions |
| POST `/finance/tax-policies` | Finance | `{rule,reason}`; creates inactive draft |
| POST `/finance/tax-policies/{id}/approve` | Different Finance user | `{reason}`; author cannot approve; same-scope period overlap conflicts |
| POST `/finance/tax-policies/{id}/retire` | Finance | `{reason}`; immutable retirement; never silently reactivates fallback |
| GET `/reports/{id}/lines/{line}/calculation-history?page=1` | Report scope | 25 versions/page, accepted summaries and field-level before/after differences |
| GET same path with `before={guid}&after={guid}` | Report scope | Compare two versions belonging to that exact receipt/report |
| GET `/reports/{id}/resets` | Report scope | Immutable reset and restore history |
| POST `/reports/{id}/reset` | Employee edit or Finance edit | `{version,confirmation:reportNumber,reason}`; archives active lines/groups; originals retained; bank matches must be undone first |
| POST `/reports/{id}/resets/{reset}/restore` | Same edit rules | Same command; empty target only; restores header/lines/groups; financial confirmations invalidated |
| GET `/finance/bank-treatments` | Finance | Treatment and immutable revocation history |
| POST `/finance/transactions/{id}/treatment` | Finance review | `{reportId,version,kind,principalRon,feeRon,markupRon,evidenceId,category,costCenter,reason}` |
| POST `/finance/bank-treatments/{id}/undo` | Finance review | `{version,reason}`; all related allocations must first be undone; generated fee lines archived, source/history retained |
| GET `/finance/acceptance` | Finance | Current configuration hash, broken/sample-reference findings and acceptance history |
| POST `/finance/acceptance` | Finance | `{company,configurationHash,totalsChecked,sourceChecked,roundingChecked,erpChecked,reason}`; actual checks and current hash required |

Existing calculate-preview, verify-financials, FX proposal/decision, matching and accounting export endpoints remain in place. Choosing bank FX requires an explicit current classification for every allocated statement transaction, including NORMAL/no-charge cases. Statements may be attached to a receipt or report; linking a receipt attachment to fee expenses reuses its immutable source without a false all-receipts declaration.

## Rule contract

`TaxPolicyRule`: company (`DEFAULT` before a company CUI is configured, configured CUI or `*`), countryGroup (`RO`, `FOREIGN`, `*`), documentKind/code or `*`, companyCui (`PRESENT`, `ABSENT`, `*`), category/code or `*`, from/to, priority, inputVatRequired, allowZeroBase, treatment, authority and `deductibility=CATEGORY_MAPPING`.

Approved rules resolve by effective date, highest priority then most specific scope. Equal priority/specificity ties or gaps block. Only an installation/company that has never activated a controlled policy may use the explicit owner fallback. Retired policies remain reproducible in old snapshots. The current app remains one internal company context; this is not a new multi-tenant identity or card-owner module.

Treatments: `STANDARD`, `EXEMPT`, `OUTSIDE_SCOPE`, `NOT_PRESENT`; unknown remains blocked. Special treatment needs a matching approved policy and an explanation. Zero-rate base always requires source evidence; no inferred residual. Category accounting, including vehicle splits, remains explicit reference data. 5% and 19% cannot be enabled through policy.

`TaxData` retains rateId/base/vat and adds optional evidence state, reference and page. State: unknown, not-present, explicit-zero, printed amount, exempt, outside-scope. Unsupported/unknown component use blocks; document-level exemption/outside-scope requires the special policy. Entered pages must belong to verified source grouping. Accepted `VerifiedVatComponent` rows are unique per calculation/rate and pin source hashes/regions/verification.

## Decimal wire protocol

The browser sends `X-Money-Format: exact-v1`. Decimal fields are serialized as invariant strings and monetary input accepts canonical strings. Integers/counts/IDs retain their types. Legacy callers without the header continue receiving numeric decimal fields. Legacy immutable snapshot/idempotency JSON is read losslessly by the browser's monetary-token parser. Financial arithmetic and formatting use BigInt-scaled decimal values; no browser floating-point conversion is used to book or compare money. Original currency precision is checked, never silently rounded on entry/import; RON allocation uses controlled rounding with final-allocation cent conservation.

Internal archived JSON and original export files are not rewritten. Direct snapshot downloads retain their original bytes and hash contract.

## Production gate

Production export requires an actual Finance acceptance record matching the current financial configuration hash. Draft tax policies alone do not invalidate acceptance. Scoped policy approvals/retirements, mapping/currency version changes do. Per-expense FX choices do not alter that configuration hash and never request manager reapproval. Test/development workflows can validate controlled synthetic fixtures without impersonating production sign-off.

Source transaction imports, tax drafts/decisions, accepted components/calculations, bank treatments/revocations, resets/restores and acceptance records are append-only. Schema upgrade 8 installs table/index/foreign-key changes and database immutability triggers.
