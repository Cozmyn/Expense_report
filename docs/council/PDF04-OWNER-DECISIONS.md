# PDF04 — decizii beneficiar / owner decisions
06.11.2026. These explicit decisions supersede PDF04 design/open questions.

| Decision | RO | EN | Application |
|---|---|---|---|
| D04-01 | Numai Finance/contabilitate descarcă fișierele Excel contabile. Auditorii păstrează accesul autorizat la originale și PDF-uri. | Only Finance/accounting downloads accounting Excel files. Authorised Auditors retain original/PDF access. | Existing Finance role; both XLSX endpoints restricted; no new role. |
| D04-02 | Verificăm baza, TVA-ul și totalul documentului. Import păstrează structura și sumele Excel; nu adăugăm un model TVA al ERP sau alte împărțiri. | Check source base/VAT/gross. Preserve workbook Import structure and amounts; no additional ERP VAT model or splits. | Source-like strategies and exact A:X; printed bases are retained after checking. Never force raw journal totals to match by mutating amounts. |
| D04-03 | Cheltuielile auto au implicit 50% deductibilitate, indiferent de card/plată. Finance poate modifica procentul. | Vehicle expenses default to 50% deductibility regardless of card/payment. Finance can change it. | Dated append-only configuration versions, default50; custom non50 VAT codes must be configured, never silently reuse50 codes. |

The default preserves the existing workbook's vehicle rows, including its original half/quarter rounding and category accounts. The editable percentage changes those established vehicle allocations deliberately; it is not a new ERP tax-calculation model. For RO without company VAT ID, preserve the workbook gross-only row; classify its vehicle deductibility without inventing recoverable VAT or extra split rows.

Earlier owner decisions stay binding: ≤50 active receipts, separate payment reports, disabled5%/19%, expense employee Worker captured in header, no card-owner/OIDC feature, Finance-only FX/rounding, no manager reapproval for FX-only changes, documented zero base, no extra attachment for≤2RON rounding, signed amounts in the same workflow with warnings.

PDF04's more-than87 fixture becomes50/51 boundary plus serializer>999 rows. Its5% pass fixtures become explicit rejection cases, with supported mixed components tested separately. CSV, a selectable ERP-generated VAT model, per-card vehicle rates, automatic ERP posting/reversal and fabricated approval dates are not introduced. Authenticated download streaming replaces shareable signed links and retains audit/role restrictions.

Real ERP acceptance is still distinct from implementation tests. The original workbook has no populated golden Import rows; synthetic fixtures are labelled and actual approval dates remain null until performed.
