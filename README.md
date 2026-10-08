# Decont — platformă de cheltuieli / expense platform

[![Validate expense platform](https://github.com/Cozmyn/Expense_report/actions/workflows/verify.yml/badge.svg)](https://github.com/Cozmyn/Expense_report/actions/workflows/verify.yml)

**Stare / Status: backend și frontend compilate în GitHub Actions; 38 teste backend trecute pe SQLite și PostgreSQL; 5 teste frontend trecute; containerul construit și pornit. Două fluxuri de browser trecute (desktop și mobil), cu ClamAV real, PDF, ZIP și export ERP. Nu este o lansare publică pentru producție.**

## Ce conține / What's included

Interfață React + TypeScript, în română implicit, cu schimbare în engleză; dashboard, deconturi, editor de cheltuieli, documente originale, aprobare manager, verificare Finance, furnizori, carduri, nomenclatoare, utilizatori, delegări și audit. Design responsive, verde profund, alb și tonuri deschise, cu navigation persistent și formulare cu etichete.

Backend ASP.NET Core 10 + Identity; PostgreSQL pentru implementare, SQLite numai în development/test; sesiuni păstrate pe server, protecție CSRF, roluri și acces pe decont, versiuni concurente, jurnal și revizii, coadă de joburi, ClamAV, OCR Tesseract, SMTP, stocare privată locală în development sau S3 în producție. Motor ERP cu 24 de coloane și fișiere XLSX memorate, hash-uri, istoric de export și confirmare de postare. PDF bilingv și arhivă cu originale.

Romanian is the default application language. The English switch changes labels without changing financial data or rules. The backend uses application-managed credentials and permissions. The repository includes the source, tests and container configuration. GitHub Actions builds the frontend and publishes application artifacts; no public production deployment or production credentials are included.

## Bonuri fiscale / Receipt workflow

Platformă internă, fără pagină de vânzare sau sloganuri. Un decont conține până la 50 de bonuri/documente, fiecare cu propriul tip de cheltuială, dată, descriere, monedă, sumă și alocare contabilă.

- **Furnizor din străinătate:** nu se cer CUI și TVA românesc. Se introduc descrierea, moneda și suma; cursul intern datat calculează valoarea RON. Marcajul intern `ZZ` înseamnă furnizor străin, țară nespecificată; nu inventează țara furnizorului.
- **Furnizor din România, fără CUI-ul companiei pe bon:** descriere, monedă și sumă; fără CUI furnizor sau defalcare TVA.
- **Furnizor din România, cu CUI-ul companiei pe bon:** CUI furnizor, verificare în baza internă și număr bon. Furnizorul existent se afișează automat. Pentru un CUI valid, dar necunoscut, angajatul verifică și confirmă corectitudinea; Finance primește ulterior o solicitare de înregistrare. Mai multe bonuri ale aceluiași furnizor într-un decont creează o singură solicitare.
- **TVA pentru bonurile cu CUI-ul companiei:** se copiază separat baza fără TVA și TVA-ul efectiv plătit la 0%, 11% și 21%. Totalul se recompune și se verifică față de suma bonului, cu diferență de maximum 0,01. TVA 0% trebuie să fie zero; TVA la 11%/21% este verificat aritmetic, cu toleranța de rotunjire de maximum 1 unitate afișată în formular. Mapările trebuie să fie active la data bonului. Regulile configurabile nu pot elimina câmpurile contabile obligatorii ale acestui flux.
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
3. Cote TVA 0/11/21, perioade, grupul TVA, codurile ordinare și codurile 50% pentru auto. 5% este blocat pentru tranzacții noi.
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

GitHub Actions verifies the production frontend build, five frontend tests, 38 backend tests on both SQLite and PostgreSQL, backend publish and container launch. Tests cover CSRF, object permissions, disabling accounts, logout, stale writes, idempotency, card allocation overflow and actual settlement, immutable audit, VAT/vehicle synthetic cases and the exact values-only XLSX schema. Two Playwright workflows pass on desktop Chromium and a 390×844 mobile viewport: RO/EN login, forced password change, expense entry, real receipt scan, submit, manager approval, Finance review, duplicate exception, ERP XLSX export/posting, PDF and original-document ZIP download. Manager and Finance decisions use authenticated HTTP clients against the same running app; their full UI interactions are not covered by these two scenarios. The initial local sandbox runtime failure is bypassed by the functioning GitHub runner.

Read `docs/STATUS.md`, `docs/REQUIREMENTS.md` and `docs/DEPLOYMENT.md` before deciding readiness. Several requirements are partial or still missing. Actual ERP golden files and operational acceptance tests are required before production. Automated verification covers the listed workflows; it does not establish every business scenario, production SLA or live ERP compatibility.
