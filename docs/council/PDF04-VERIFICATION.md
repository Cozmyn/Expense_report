# PDF04 verification / verificare

06.11.2026. Owner contract: D04-01/02/03. Local SQLite suite237 passed,0 failed/skipped; UI9 passed; TypeScript/production build passed. CI PostgreSQL/browser/container validation is in progress on branch council/pdf04-posting; this is not yet the final release evidence.

Tests cover14 independently expected A:X fixtures (5% cases adapted to rejection), source base/VAT preservation, odd-cent original50% rounding, editable0/25/75/100 percentages with required tax codes, payment independence, signed same-flow entries,50/51 cap and serializer>999 rows, canonical persistence hashes, Excel precision rejection, Finance-only percentage/Excel controls, Auditor originals/PDF, stale mapping/preview rejection, business retry, normalized immutable provenance, once-per-expense SQL totals and repeatable legacy-preserving schema9.

CI verification artifacts include SYNTHETIC-NOT-FOR-PRODUCTION ERP reference XLSX files, fixture JSON and calculation/trace JSON. These are regression artifacts, not actual approved ERP imports. Browser coverage now includes source-linked frozen posting preview, accounting dashboard, future percentage editing and request-key-independent export replay on desktop/mobile alongside existing OCR and approval flows.

Real company mappings/representative source documents/ERP acceptance remain human gates. Source XLSM has no populated golden output; actual approval dates remain null. Run init for schema9 before upgraded deployment. No live production deployment or actual ERP posting is claimed.
