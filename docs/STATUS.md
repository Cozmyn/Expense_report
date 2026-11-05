# Starea curentă / Current status

**PDF 02 update (05.11.2026):** current implementation/evidence is in `council/BLUEPRINT-02-CURRENT-MATRIX.md` and `council/VERIFICATION-02.json`. Earlier slice findings below are historical; external acceptance remains explicit.

**Implementarea completă a cererii premium nu este terminată. / The full premium request is not complete.**

Auditul static și planul de reproiectare sunt livrate; verificarea completă și implementarea tuturor loturilor rămân deschise. Corecțiile financiare și profilul RO/EN sunt documentate în [loturile 1–2](council/PHASE-3-BATCH-1-2.md). Rezultatele actuale sunt în [VERIFICATION-CURRENT.json](council/VERIFICATION-CURRENT.json).

Folosește [matricea actuală](council/PHASE-3-MATRIX.csv) și [verificarea cererii originale](council/ORIGINAL-REQUEST-CHECK.md) pentru acceptanță. Tabelul istoric de mai jos descrie funcții existente, nu dovedește 100% acoperire sau lipsa defectelor.

## Istoric verificat anterior / Earlier verified baseline

# Starea implementării / Implementation status

2026-10-08. Platformă internă, română implicit și engleză, pentru o singură entitate juridică din România. Nu este publicată o instanță de producție.

## Verificare / Verification

Sursa verificată: `13257f9c5f6a4814a25e10f671646a58bbf4d800`.
[GitHub Actions](https://github.com/Cozmyn/Expense_report/actions/runs/37783990281).
Actualizările ulterioare ale acestui document și README sunt exclusiv documentație; nu modifică sursa verificată.

- 63 teste backend pe SQLite și 63 pe PostgreSQL, fără teste omise.
- 5 teste frontend, verificare TypeScript, build Vite și audit npm.
- Două fluxuri Playwright: desktop Chromium și mobil 390×844, cu aplicația reală, ClamAV și OCR Tesseract pentru un PDF de două pagini.
- Build Docker, bootstrap repetat fără resetarea contului existent, health/static assets și acces anonim respins.

Browser tests enter receipts and confirm OCR suggestions through the UI. Manager/Finance decisions, supplier resolution, accounting exports and downloads use authenticated HTTP clients against the running application. These two scenarios do not cover every administrative screen interaction.

## Funcții implementate / Implemented functions

| Domeniu / Area | Implementare / Implementation |
|---|---|
| Bonuri și documente | Până la 50 de bonuri; cele trei ramuri RO/străin/CUI companie; lookup CUI, confirmare necunoscut; baze și TVA plătit pe cote; documente individuale/comune cu acoperire explicită; scanare obligatorie |
| OCR asistat | Încărcare înainte de formular; procesare asincronă după scanare; toate paginile PDF; sursă/pagină/stare ReviewRequired; confirmare explicită; câmpurile lipsă rămân goale; reîncercare și completare manuală; conversie HEIC |
| Experiență utilizator | Formular lângă document pe desktop, responsive pe mobil; browse/cameră/progres; ciornă persistentă; protecție pentru modificări nesalvate; totaluri pe monedă și RON; limbi RO/EN |
| Aprobări | Inbox propriu/delegat, comentarii pe bon, revizii și hash; respingere/returnare cu motiv; corecții Finance auditate; reaprobare configurabilă pentru creșterea decontării personale |
| Finance | Filtre și sortare salvate, excepții și aging; corecții angajat/manager cu reaprobare; GL/local/subcont și TVA override cu motiv; preflight; furnizori cu istoric datat al conturilor ERP |
| Carduri | CSV/XLSX și profile de coloane; erori/duplicate; sugestii; alocări manuale/parțiale/multiple; anulare cu motiv; decontare bancară efectivă RON; titular separat de persoana care a efectuat cheltuiala |
| ERP și documente | Preview și XLSX exact 24 coloane, worksheet Import; exporturi memorate/hash/nume/istoric; postare și redeschidere cu permisiune și reversare ERP externă; Bonuri/TVA XLSX, PDF Unicode, ZIP cu originale |
| Administrare | Utilizatori/roluri/disable/unlock/reset; parole temporare; reset single-use prin SMTP; inactivitate sesiuni; nomenclatoare și țări/documente/cote viitoare datate; importuri CSV/XLSX tranzacționale; organizație/alocări/delegări |
| Operațiuni | Notificări pe eveniment și canal; remindere/escaladare deduplicate; retry/backoff; stare joburi/dependențe; audit căutabil/exportabil; raportare și dashboard-uri în limitele rolului |
| Integritate | CSRF, RBAC și scope, concurență/idempotency; originale private; audit/revizii/decizii protejate; upgrade-uri aditive versionate/repetabile pe ambele baze |

Source-to-requirement evidence is in [REQUIREMENTS.md](REQUIREMENTS.md); configuration and launch guidance are in [DEPLOYMENT.md](DEPLOYMENT.md) and the README.

## Acceptanță operațională / Operational acceptance

The implemented application and passing tests are not a certification of every company scenario or a production SLA.

- Company Finance must supply and approve actual ERP accounts, tax codes, posting mappings and expected import files. Synthetic tests do not establish live ERP compatibility. External ERP reversal is recorded by reference; the application does not execute that reversal.
- SMTP configuration and real delivery, TLS/domain, private object storage/IAM/encryption, key handling, alerts, database backups/PITR and timed restore drills require the deployment environment.
- Security/ASVS, accessibility/WCAG, company-volume load/performance and user acceptance require dedicated production-like validation.
- OCR is assisted extraction. It does not supply calibrated numeric confidence, invent missing fields or automatically submit. Real Romanian receipt variety needs user acceptance; HEIC conversion is implemented but no real HEIC fixture was tested.
- Imports use bounded synchronous preview/confirmation (5 MB, 10,000 rows); OCR and scans use background jobs. Chunked/resumable uploads are not implemented.
- Original retention/legal hold are recorded; no automatic physical purge is performed. PDF is generated from the report and is not digitally signed.
- Additive upgrades are tested and versioned; they are not a complete EF migration/rollback framework. Native launch scripts have not been executed on user devices.
- No multi-company tenancy, SSO/MFA, direct live ERP connector or public hosting is included.

