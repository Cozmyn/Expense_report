# Starea reconstrucției / Rebuild status

[Verificare CI reușită / Successful CI](https://github.com/Cozmyn/Expense_report/actions/runs/37744234244).

2026-10-08. Sursă nouă, fără dependență de ZIP-ul anterior. Build-ul, testele backend și pornirea containerului sunt validate în GitHub Actions. Nu există încă o lansare publică pentru producție.

| Pas / Step | Rezultat / Result | Verificare / Verification |
|---|---|---|
| 1. Cerințe și structură | Documentația disponibilă și cerințele urmărite în REQUIREMENTS.md | Documentele citite; Excel consultat punctual |
| 2. Auth, roluri, persistență | Identity, sesiuni DB, CSRF, RBAC, PostgreSQL/SQLite, RO/EN | 25 teste backend trecute pe SQLite/PostgreSQL |
| 3. Deconturi și aprobări | CRUD, limite, atașamente, snapshots, aprobare/returnare/reaprobare | 25 teste backend trecute pe SQLite/PostgreSQL |
| 4. Finance și administrare | Furnizori, utilizatori, delegări, versiuni nomenclatoare, CSV și alocări card | Cod prezent; unele cerințe parțiale |
| 5. ERP și documente | Preflight, preview, XLSX 24 coloane, batch memorat, PDF, ZIP, raport CSV | XLSX, PDF și ZIP descărcate în fluxuri reale CI; acceptanța ERP a companiei în așteptare |
| 6. Verificare și pachet | Frontend construit; 5 teste frontend, 25 teste backend și pornire Docker trecute | Backend verificat în GitHub; două fluxuri desktop/mobil trecute; nicio lansare publică |

Browser acceptance runs employee UI entry on desktop and mobile. Manager/Finance decisions, export and downloads are exercised through authenticated HTTP clients against the live application, with a real ClamAV scan; these tests do not exercise every administrative screen.

## Limitări concrete / Concrete limitations

- Runtime-ul sandbox-ului local nu pornește; GitHub Actions a restaurat, compilat și testat backend-ul cu succes.
- Nu există URL public sau cont de producție. Local launch scripts need a working Docker installation. Docker image build, repeated bootstrap and container startup have passed in CI; native PowerShell/bash launch scripts have not themselves been executed on user devices.
- ERP tests are synthetic. The company must approve tax codes/account mappings and compare actual expected import files, especially vehicle 11%, mixed VAT and small 0% amounts. The legacy vehicle 11% branch is not copied blindly.
- OCR extracts text with a manual-verification label; structured field extraction, confidence scores and prefill are not implemented. PDF OCR currently processes page one; HEIC OCR needs a supported converter.
- Bank statement import supports CSV; XLSX statement import and stored mapping profiles remain to implement. Partial and multi-allocation backend logic is present; the UI confirms suggested lines only and does not yet expose undo.
- Unicode PDF generation and PDF/ZIP HTTP downloads are runtime-tested. Visual layout acceptance with long, real documents remains pending. PDF is generated from the current report, not digitally signed.
- Saved Finance filters, complete live exception dashboards, rich transaction-currency totals, line-specific manager comments and comprehensive report grouping remain incomplete.
- Admin password reset exists; self-service email reset is not implemented. Session timeout is absolute, not idle. Full history of organization routing is not implemented.
- Notification job retries and in-app notices exist; not every requested event is covered. Scheduled reminders/escalation and configurable notification policies remain to implement.
- Master CSV/XLSX bulk import, configurable countries/document types, external VAT verification and future VAT-rate activation remain to implement.
- Retention is recorded at posting and originals are not physically deleted. No automated retention purge is implemented; original documents survive line archival and legal hold is recorded.
- Initial database creation uses EnsureCreated. Versioned EF migrations, deployment/rollback automation, storage encryption/IAM, key-vault rotation, monitoring alerts and restore drills must be completed and tested before production.
- No multi-company tenancy, live ERP integration, SSO, MFA or public hosting is implemented. These are not implied by the local build.
- Source authorization has automated HTTP tests. Full security review, accessibility, load and disaster-recovery acceptance remain outstanding.
