# Blueprint 01 — decizii confirmate / confirmed decisions

Confirmed by the user, November 2026:
- Maximum 50 active receipts per report. Workbook's 87 rows do not increase the product limit.
- 5% VAT disabled, including historical entry; current server validation retained.
- Separate reports for personal payments and company-card expenses.
- No enterprise OIDC; keep username/password.
- Accounting Worker stays the employee incurring expenses, not card owner.

## Implementare / Implementation

The first receipt establishes payment method. Subsequent receipt editors inherit and lock it while other active receipts exist. The shared Apply service rejects a conflicting payment method with mixed_payment before modifying financial data. Preflight blocks mixed legacy reports for submission/export. Archived receipts do not constrain payment. Existing records are not split or deleted automatically. A sole receipt can change method; once all receipts are archived, a new first receipt can establish another method.

RO: Plata personală și cardul companiei necesită deconturi separate. Primul bon stabilește metoda de plată. Pentru schimbarea metodei când există alte bonuri, creează un decont separat.
EN: Personal and company-card payments require separate reports. The first receipt establishes payment method. Create another report to use a different method when other receipts exist.

Files: Reports.cs, Rules.cs, main.tsx, ReceiptModal.tsx, BusinessTests.cs. Tests added: conflicting service write rejected without changing line data; matching payment accepted despite archived opposite payment; mixed legacy preflight blocked. CI pending. Report-header payment selection remains a follow-up blueprint task; this batch enforces the separation using active receipts.

Prior blanket mixed-payment behavior is superseded by the explicit user decision. No other requirement is retired. Blueprint source SHA-256 confirmed against attached workbook. Other blueprint work and complete field-level traceability remain open.
