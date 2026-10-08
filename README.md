# Decont — platformă de cheltuieli / expense platform

**Stare / Status: implementare nouă în sursă; frontend verificat; backend necompilat în mediul de creare. Nu este o lansare validată pentru producție.**

## Ce conține / What's included

Interfață React + TypeScript, în română implicit, cu schimbare în engleză; dashboard, deconturi, editor de cheltuieli, documente originale, aprobare manager, verificare Finance, furnizori, carduri, nomenclatoare, utilizatori, delegări și audit. Design responsive, verde profund, alb și tonuri deschise, cu navigation persistent și formulare cu etichete.

Backend ASP.NET Core 10 + Identity; PostgreSQL pentru implementare, SQLite numai în development/test; sesiuni păstrate pe server, protecție CSRF, roluri și acces pe decont, versiuni concurente, jurnal și revizii, coadă de joburi, ClamAV, OCR Tesseract, SMTP, stocare privată locală în development sau S3 în producție. Motor ERP cu 24 de coloane și fișiere XLSX memorate, hash-uri, istoric de export și confirmare de postare. PDF bilingv și arhivă cu originale.

Romanian is the default application language. The English switch changes labels without changing financial data or rules. The backend uses application-managed credentials and permissions. This package includes the source, built frontend, tests and container configuration; it does not contain a hosted application or production credentials.

## Pornire locală / Local launch

Necesită Docker Desktop cu Linux containers sau Docker Engine + Compose. Scriptul creează parole locale aleatorii în `.env`; nu încărca acel fișier într-un repository. Scripturile și build-ul backend nu au putut fi executate în mediul de creare.

- Windows: deschide PowerShell în acest director și rulează `./Start-Local.ps1`.
- Linux/macOS: rulează `bash ./start-local.sh`.
- Alternativ: copiază `.env.example` în `.env`, înlocuiește parolele și datele de admin, apoi rulează:

```sh
docker compose up -d db scanner
docker compose --profile setup run --rm init
docker compose up -d web
```

Deschide **http://localhost:5080** după finalizarea build-ului. Folosește contul configurat în `.env`; prima autentificare cere schimbarea parolei. Administratorul creează utilizatorii, configurează rolurile și managerii. Un cont care trebuie să creeze deconturi are și rolul Employee. Bootstrap-ul refuză crearea unui admin deja existent: nu îl repeta la fiecare pornire.

The container build runs backend tests before publishing. If the build fails, the application will not start; preserve the first failing error for diagnosis. No Docker container was started during creation. Scanner definition download may delay attachment scanning on first launch. Failed jobs are visible to Admin; files stay quarantined until a definitive clean verdict.

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

Frontend: TypeScript + Vite build passed; five request/CSRF/error-handling tests passed. Backend: SDK 10 downloaded but CoreCLR failed to initialise with `HRESULT 0x8007000E`, exit 137. No successful restore, compilation, API test execution, browser workflow test or Docker build is claimed. Backend tests cover permissions, CSRF, idempotent creation, immutable audit, monetary rounding and selected synthetic ERP cases; they remain unexecuted.

Read `docs/STATUS.md`, `docs/REQUIREMENTS.md` and `docs/DEPLOYMENT.md` before deciding readiness. Several requirements are partial or still missing. Actual ERP golden files and operational acceptance tests are required before production. The current source must not be represented as “100% working”.
