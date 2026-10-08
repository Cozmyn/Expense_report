# Starea reconstrucției / Rebuild status

2026-10-08. Sursă nouă, fără dependență de ZIP-ul anterior. Nu este încă o platformă validată pentru producție.

| Pas / Step | Rezultat / Result | Verificare / Verification |
|---|---|---|
| 1. Cerințe și structură | Documentația disponibilă și cerințele urmărite în REQUIREMENTS.md | Documentele citite; Excel consultat punctual |
| 2. Auth, roluri, persistență | Identity, sesiuni DB, CSRF, RBAC, PostgreSQL/SQLite, RO/EN | Cod prezent; backend neexecutat |
| 3. Deconturi și aprobări | CRUD, limite, atașamente, snapshots, aprobare/returnare/reaprobare | Cod prezent; backend neexecutat |
| 4. Finance și administrare | Furnizori, utilizatori, delegări, versiuni nomenclatoare, CSV și alocări card | Cod prezent; unele cerințe parțiale |
| 5. ERP și documente | Preflight, preview, XLSX 24 coloane, batch memorat, PDF, ZIP, raport CSV | Cod prezent; compatibilitatea ERP nevalidată |
| 6. Verificare și pachet | Frontend construit, cinci teste frontend trecute, teste backend și Docker furnizate | Backend blocat de runtime; nicio lansare live |

## Limitări concrete / Concrete limitations

- Backend-ul nu a putut fi restaurat/compilat/testat aici. CI și Docker sunt oferite pentru verificare într-un mediu .NET funcțional; nu s-a executat niciun job CI.
- Nu există URL public sau cont de producție. Local launch scripts need a working Docker installation and have not been executed.
- ERP tests are synthetic. The company must approve tax codes/account mappings and compare actual expected import files, especially vehicle 11%, mixed VAT and small 0% amounts. The legacy vehicle 11% branch is not copied blindly.
- OCR extracts text with a manual-verification label; structured field extraction, confidence scores and prefill are not implemented. PDF OCR currently processes page one; HEIC OCR needs a supported converter.
- Bank statement import supports CSV; XLSX statement import and stored mapping profiles remain to implement. Partial and multi-allocation backend logic is present; the UI confirms suggested lines only and does not yet expose undo.
- Official PDF generation is implemented in source with Unicode fonts; it has not been rendered or verified. PDF is generated from the current report, not digitally signed.
- Saved Finance filters, complete live exception dashboards, rich transaction-currency totals, line-specific manager comments and comprehensive report grouping remain incomplete.
- Admin password reset exists; self-service email reset is not implemented. Session timeout is absolute, not idle. Full history of organization routing is not implemented.
- Notification job retries and in-app notices exist; not every requested event is covered. Scheduled reminders/escalation and configurable notification policies remain to implement.
- Master CSV/XLSX bulk import, configurable countries/document types, external VAT verification and future VAT-rate activation remain to implement.
- Retention is recorded at posting and originals are not physically deleted. No automated retention purge is implemented; original documents survive line archival and legal hold is recorded.
- Initial database creation uses EnsureCreated. Versioned EF migrations, deployment/rollback automation, storage encryption/IAM, key-vault rotation, monitoring alerts and restore drills must be completed and tested before production.
- No multi-company tenancy, live ERP integration, SSO, MFA or public hosting is implemented. These are not implied by the local build.
- Source authorization is implemented, but security review, accessibility/visual browser QA, load and disaster-recovery tests remain outstanding.
