# Decont — platformă de cheltuieli / expense platform

[![Validate expense platform](https://github.com/Cozmyn/Expense_report/actions/workflows/verify.yml/badge.svg)](https://github.com/Cozmyn/Expense_report/actions/workflows/verify.yml)

**Stare / Status: platformă internă RO/EN cu bonuri multiple, OCR asistat, aprobări, Finance, reconciliere, exporturi și administrare. Verificarea exactă și dependențele de acceptanță sunt în `docs/STATUS.md`.**

## Audit și reproiectare / Audit and redesign

Cererea premium nu este complet implementată. Vezi [starea exactă](docs/council/ORIGINAL-REQUEST-CHECK.md), [planul RO](docs/council/PHASE-2-RO.md), [planul EN](docs/council/PHASE-2-DESIGN.md) și [matricea curentă](docs/council/PHASE-3-MATRIX.csv). Profilul RO/EN și corecțiile financiare sunt primele loturi livrate; restul cerințelor rămân urmărite individual.

## Ce conține / What's included

Interfață React + TypeScript, în română implicit, cu schimbare în engleză; dashboard, deconturi, editor de cheltuieli, documente originale, aprobare manager, verificare Finance, furnizori, carduri, nomenclatoare, utilizatori, delegări și audit. Design responsive, verde profund, alb și tonuri deschise, cu navigation persistent și formulare cu etichete.

Backend ASP.NET Core 10 + Identity; PostgreSQL pentru implementare, SQLite numai în development/test; sesiuni păstrate pe server, protecție CSRF, roluri și acces pe decont, versiuni concurente, jurnal și revizii, coadă de joburi, ClamAV, OCR Tesseract, SMTP, stocare privată locală în development sau S3 în producție. Motor ERP cu 24 de coloane și fișiere XLSX memorate, hash-uri, istoric de export și confirmare de postare. PDF bilingv și arhivă cu originale.

Romanian is the default application language. The English switch changes labels without changing financial data or rules. The backend uses application-managed credentials and permissions. The repository includes the source, tests and container configuration. GitHub Actions builds the frontend and publishes application artifacts; no public production deployment or production credentials are included.

## Bonuri fiscale / Receipt workflow

Platformă internă, fără pagină de vânzare sau sloganuri. Un decont conține până la 50 de bonuri/documente, fiecare cu propriul tip de cheltuială, dată, descriere, monedă, sumă și alocare contabilă.

- **Furnizor din străinătate:** nu se cer CUI și TVA românesc. Se introduc descrierea, moneda și suma; cursul intern datat calculează valoarea RON. Marcajul intern `ZZ` înseamnă furnizor străin, țară nespecificată; nu inventează țara furnizorului.
- **Furnizor din România, fără CUI-ul companiei pe bon:** descriere, monedă și sumă; fără CUI furnizor sau defalcare TVA.
- **Furnizor din România, cu CUI-ul companiei pe bon:** CUI furnizor, verificare în baza internă și număr bon. Furnizorul existent se afișează automat. Pentru un CUI valid, dar necunoscut, angajatul verifică și confirmă corectitudinea; Finance primește ulterior o solicitare de înregistrare. Mai multe bonuri ale aceluiași furnizor într-un decont creează o singură solicitare.
- **TVA pentru bonurile cu CUI-ul companiei:** se copiază separat baza fără TVA și TVA-ul efectiv plătit la 0%, 11% și 21%, plus cote viitoare configurate și valabile. Totalul se recompune și se verifică față de suma bonului, cu diferență de maximum 0,01. TVA 0% trebuie să fie zero; TVA la 11%/21% este verificat aritmetic, cu toleranța de rotunjire de maximum 1 RON, calculată de server inclusiv pentru bonuri în valută. Mapările trebuie să fie active la data bonului. Regulile configurabile nu pot elimina câmpurile contabile obligatorii ale acestui flux.
- **Documente:** selector de fișiere vizibil în editor și lângă fiecare bon, plus secțiune la final pentru un fișier comun. Fișierul comun necesită confirmarea că include toate bonurile existente la încărcare. Bonurile adăugate ulterior necesită documente proprii sau reconfirmarea acoperirii fișierului. Nu se poate trimite fără documente pentru toate bonurile și scanare ClamAV reușită. Fișierele originale se păstrează în dosar și în reviziile de audit.
- **Contabilitate:** Finance/Auditor poate descărca „Bonuri și TVA (XLSX)”: foaia `Bonuri` păstrează datele fiecărui document; foaia `TVA pe cote` păstrează exact baza și TVA-ul plătit pe fiecare cotă în moneda introdusă. Acesta este exportul detaliilor reviziei/versiunii curente, separat de schema ERP de 24 de coloane. PDF, HTML și JSON includ și defalcarea TVA. Exportul ERP rămâne blocat până la configurarea contului furnizorului și a mapărilor.

**English:** Every receipt has separate data and evidence. Foreign suppliers and Romanian receipts without the company VAT ID use the description/currency/amount branch. Romanian receipts showing the company VAT ID require supplier lookup and per-rate taxable bases and paid VAT. Unknown suppliers require explicit confirmation and later Finance registration. Submission requires scanned individual files or a confirmed combined file covering all receipts. Finance receives a detailed receipts/VAT workbook alongside the existing ERP import. Romanian is the default; English labels follow the same rules.

## Pornire locală / Local launch

Necesită Docker Desktop cu Linux containers sau Docker Engine + Compose. Scriptul creează parole locale aleatorii în `.env`; nu încărca acel fișier într-un repository. Build-ul backend, testele și pornirea containerului sunt verificate în GitHub Actions.

- Windows: deschide PowerShell în acest director și rulează `./Start-Local.ps1`.
- Linux/macOS: rulează `bash ./start-local.sh`.
- Alternativ: copiază `.env.example` în `.env`, înlocuiește parolele și datele de admin, apoi rulează:

```sh
docker compose up -d db scanner
docker compose --profile setup run --rm init
docker compose up -d web
```

Deschide **http://localhost:5080** după finalizarea build-ului. Folosește contul configurat în `.env`; prima autentificare cere schimbarea parolei. Administratorul creează utilizatorii, configurează rolurile și managerii. Un cont care trebuie să creeze deconturi are și rolul Employee. La o pornire repetată, bootstrap-ul păstrează contul administrativ existent și nu schimbă parola.

The container build runs backend tests before publishing. GitHub Actions builds and starts the container, verifies health/static assets and requires HTTP 401 for anonymous report access. Scanner definition download may delay attachment scanning on first launch. Failed jobs are visible to Admin; files stay quarantined until a definitive clean verdict.

## Configurare financiară / Finance setup

Nu sunt introduse conturi contabile, cursuri, furnizori sau carduri fictive. Înainte de submit/export configurează:

1. Conturile angajaților, codurile Worker și managerul de aprobare.
2. Tipuri de cheltuieli, centre de cost și proiecte cu codurile ERP explicite.
3. Cote TVA 0/11/21, perioade, grupul TVA, codurile ordinare și codurile 50% pentru auto. 5% și 19% sunt dezactivate în configurarea activă și în contabilizare.
4. Mapări contabile pe categorie: GL, cont local, subcont și clasificare auto/reparații.
5. Cursuri interne datate pentru fiecare monedă străină; carduri active și titulari.
6. Furnizori și conturi ERP; furnizorii lipsă creează solicitări la submit.
7. Setarea ERP `default`: conturile de decontare/TVA/nedeductibile și referința aprobării regulilor de către Finance.

Configurațiile au perioade de valabilitate fără suprapunere. Pentru schimbări creează versiuni noi. Închide perioada versiunii anterioare înainte de a crea versiunea nouă. Document date selects applicable mappings. Worker is always the employee who incurred the expense, including when a card belongs to someone else.

## Verificare / Verification

```sh
npm ci --prefix ui
npm test --prefix ui
npm run build --prefix ui
dotnet test tests/Decont.Tests.csproj --configuration Release
```

GitHub Actions verifies the frontend build and tests, the backend suite on both SQLite and PostgreSQL, release publish and container launch. Tests cover CSRF, object permissions, disabling accounts, logout, stale writes, idempotency, card allocation overflow and actual settlement, immutable audit, VAT/vehicle synthetic cases and the exact values-only XLSX schema. Two Playwright workflows pass on desktop Chromium and a 390×844 mobile viewport: RO/EN login, forced password change, expense entry, real receipt scan, submit, manager approval, Finance review, duplicate exception, ERP XLSX export/posting, PDF and original-document ZIP download. Manager and Finance decisions use authenticated HTTP clients against the same running app; their full UI interactions are not covered by these two scenarios. A second browser scenario uploads a real two-page PDF, runs Tesseract after ClamAV, reviews prefilled values, creates two receipts and checks original-file coverage.

Read `docs/STATUS.md`, `docs/REQUIREMENTS.md` and `docs/DEPLOYMENT.md` before deciding readiness. Application functions are mapped to their source in the requirements matrix; operational acceptance is tracked separately. Actual ERP golden files and operational acceptance tests are required before production. Automated verification covers the listed workflows; it does not establish every business scenario, production SLA or live ERP compatibility.

## PDF 02 — bulk OCR / OCR pentru documente multiple

Flux implementat: încărcare semnată → scanare → normalizare/pagini → grupuri/regiuni → propuneri → verificare umană → bonuri cu trasabilitate. Romanian default, English available. A single PDF can produce multiple drafts, including a three-page invoice and two receipts on one scan. Regrouping, corrections, supporting pages, source previews and safe retry are available; submission requires complete source review.

See [current traceability](docs/council/BLUEPRINT-02-CURRENT-MATRIX.md), [API](docs/council/API-02.md), [architecture](docs/council/ADR-02.md), [verification and owner QA](docs/council/TEST-02.md) and [security/operations](docs/council/SECURITY-02.md). CI produces parser-quality JSON plus desktop/mobile video/screenshots. Synthetic regression results are not real-document accuracy guarantees. Finance quality/ERP sign-off and target infrastructure acceptance remain external release gates.

Category configuration may include `ocrKeywords` on active `expenseTypes`. Allocation policy `policies/expenseDefaults` supports company `costCenter`/`project`, then department overrides in `departments`, then employee LantID overrides in `workers`. Referenced dimensions must be effective on the receipt date. Defaults never come from OCR guesses or force a category.

## OCR, operațiuni și configurare / OCR, operations and configuration

Încarcă documentele din panoul OCR sau fotografiază bonul. Worker-ul scanează originalul înainte de procesare și citește toate paginile PDF (maximum 50, configurabil). Rezultatele propun valori cu pagina și textul sursă; starea este `ReviewRequired`, fără scor numeric de încredere inventat. Revizuiește propunerea lângă original, alege categoria/alocarea/plata și salvează. Poți completa manual sau reîncerca extragerea. HEIC folosește `heif-convert`; originalele rămân nemodificate. Previzualizarea HEIC disponibilă după scanare este un PNG separat, generat la cerere.

- Politici datate: cod `company` pentru CUI companie, `uploads` pentru `maxFileMb`/`maxReportMb`, `vat` pentru `vatZeroTolerance`, `workflow` pentru `requireCardMatchBeforeApproval`.
- Notificări: cod `default` sau codul evenimentului, cu `inApp` și `email`. Codul `reminders` folosește `afterHours`, `repeatHours`, `escalateHours`, `escalationRole`. Worker-ul evită reminderele repetate în același interval.
- Resetare parolă: configurează SMTP și `Application__BaseUrl` cu adresa reală. Linkul expiră după 15 minute și poate fi folosit o singură dată. `Session__Minutes` controlează inactivitatea; resetarea/dezactivarea revocă sesiunile.
- Importuri CSV/XLSX: selectează un fișier, verifică rândurile și confirmă. Importurile de nomenclatoare/angajați sunt tranzacționale; un lot invalid nu creează înregistrări parțiale. Pentru extrasul de card, mapează coloanele și salvează profilul; verifică erorile/duplicatele înainte de import.
- Finance: filtre salvate, comentarii pe bon, comparație cu revizia, corecții contabile cu motiv, alocări parțiale și anulare; modificarea angajatului/managerului cere reaprobare. Redeschiderea după postare cere `FinanceReopen`, motiv și referința reversării externe ERP.
- Operațiuni: Admin vede joburi și dependențe, utilizatorii autorizați văd procesarea/livrarea în istoricul decontului. Joburile eșuate se reîncearcă cu motiv. Emailurile necesită SMTP funcțional; existența unei cozi nu confirmă livrarea.

**English:** Upload, scan, extract every PDF page, review and explicitly confirm. Configure company VAT ID, dated policies, masters, notification channels and SMTP internally. Card/master XLSX imports use preview before confirmation. Routing/accounting corrections and posted-export reopening are audited. Production ERP acceptance, infrastructure configuration and recovery/load testing remain company-environment gates.


## Calcul Finance / Finance calculation update

See [Finance decisions and bilingual operating guide](docs/council/FINANCE-DECISIONS-2026-11-05.md) and [PDF03 traceability](docs/council/BLUEPRINT-03-REQUIREMENTS.md). Run `--init` for schema release 8 before starting the upgraded server. Finance explicitly confirms final FX; estimates and manager approval remain preserved.

Earlier Finance implementation evidence (historical): [prior tested commit](docs/council/BLUEPRINT-03-VERIFICATION.md).

PDF03 completion controls: [API and migration](docs/council/API-03.md), [architecture](docs/council/ADR-03.md), [traceability](docs/council/BLUEPRINT-03-REQUIREMENTS.md). Run `--init` for schema release 8. Actual Finance/ERP acceptance is required for production export; it is never created automatically by tests or deployment.


## PDF03 — verificare finală / final verification

[Rezultate, matrice și verificări pentru beneficiar / Results, traceability and owner QA](docs/council/PDF03-COMPLETION-VERIFICATION.md): 200 backend tests per database (SQLite and PostgreSQL), 9 UI tests, 6 desktop/mobile workflows and container verification passed on runtime commit `b4efb248b13e68c6b049688c2d89990aa0f060ed`. Finance has dedicated **Configurare Finance / Finance settings** navigation independent of reports. All 59 PDF03 groups are mapped; 56 Done and 3 Partial for real production data/corpus and Finance/ERP acceptance. These results do not close every earlier premium-platform requirement or claim live production deployment.
