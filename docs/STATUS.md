# Starea curentă / Current status

## PDF04 — implementare verificată / verified implementation — 06.11.2026

Implementarea PDF04 respectă deciziile beneficiarului: numai Finance descarcă Excelurile contabile; Auditor păstrează accesul autorizat la originale/PDF; Import păstrează structura Excel fără un model TVA ERP suplimentar; deductibilitatea auto are implicit50%, editabilă de Finance, indiferent de plată. Rulează init pentru schema9.

[Final verification and owner QA](council/PDF04-VERIFICATION.md):241 tests per database,9 UI tests,6 desktop/mobile workflows and container/build/audit gates passed on `355a3bf43838854d1b4a8585a3fe4e3b7d053a78`, [run 42289410204](https://github.com/Cozmyn/Expense_report/actions/runs/42289410204). [Matrix](council/PDF04-REQUIREMENTS.md):100 mapped,98 implementation Done,2 Partial for actual human/ERP acceptance. No real ERP posting or live production deployment claimed. Earlier premium-platform scope remains tracked separately below.

## PDF03 — închidere implementare / implementation closure — 06.11.2026

Implementarea software PDF03 este integrată în `main`, commit `5ef829733d067d7936872f278c6304a4c6f2bccf`. [Verificarea pe main](https://github.com/Cozmyn/Expense_report/actions/runs/42040036179) a trecut: 200 teste backend pe SQLite și 200 pe PostgreSQL (fără eșecuri sau omiteri), 9 teste UI, 6 fluxuri desktop/mobil, TypeScript/build/audit dependențe și verificarea containerului. Migrarea necesită `--init`, schema release 8.

The PDF03 software implementation is merged and the main-branch checks passed. [Final verification and owner QA](council/PDF03-COMPLETION-VERIFICATION.md) and [PDF03 traceability](council/BLUEPRINT-03-REQUIREMENTS.md) record 59/59 mapped groups: 56 Done, 3 Partial for actual production reference data, representative-document validation and Finance/ERP acceptance. These require company evidence and authenticated human acceptance; they cannot be completed with fabricated sign-off. Live production deployment and actual ERP import have not been performed.

Această închidere privește PDF03. Starea cererii premium originale și constatările istorice sunt păstrate mai jos; nu sunt declarate automat rezolvate prin livrarea PDF03. / This closure concerns PDF03; it does not mark every earlier premium-platform requirement complete.

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

