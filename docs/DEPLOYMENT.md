# Implementare / Deployment

## Local / DEV

`compose.yaml` is a local evaluation setup bound to loopback. It uses PostgreSQL, private local files and ClamAV. It sets Development mode to allow local HTTP. Do not expose this Compose configuration publicly or use its local generated admin email/password in production. Docker image build executes tests and stops on failure.

Node 22 builds the frontend. .NET 10 builds/publishes the backend. Runtime image uses the official `10.0-noble` Ubuntu variant and installs Romanian/English Tesseract, Poppler and DejaVu Unicode fonts. Image tags are mutable: pin approved digests in the release pipeline. Development setup uses reviewed versioned migrations; Production uses a separate privileged --migrate job. Do not point `--init` at an existing unrelated database.

## Control de schemă / Schema integrity gate — P0 DB-01

**Release 14:** schema nouă și actualizările legacy sunt bazate pe SQL **explicit și versionat** (fișierele `server/Migrations/`), cu SHA-256 fixat, tranzacție și lock advisory PostgreSQL. Nu se extrage DDL cu regex și nu se rulează `EnsureCreated` în migrator. VerifyModel controlează structura fizică; baza cu release 13 nu este modificată în tăcere.

**Development:** `--init` / `--bootstrap` pot aplica schema locală, pentru compatibilitatea Docker Compose. **Production:** rulați separat `--migrate` într-un job one-shot, cu identitate PostgreSQL de migrare și backup înainte, apoi `--bootstrap` cu contul runtime numai după succes; startup-ul aplicației face doar verificarea read-only a release-ului 14. `--init` este refuzat în Production.

Rehearsal-ul trebuie executat pe **copii restaurate ale tuturor versiunilor istorice efectiv folosite**, inclusiv baza release 13, cu dovezi de păstrare a rândurilor financiare, checks, FK, indecși și triggere. Scriptul de upgrade 013 este aditiv și nu reprezintă o permisiune pentru downgrade-uri distructive.

Procedura completă, exemplele de `docker run --migrate`, rolurile DB și gate-ul auditabil: [P0 release runbook](P0_RELEASE_RUNBOOK.md).

## Actualizarea unei instalări existente / Existing installation upgrade

Pentru Development local se menține comanda `docker compose --profile setup run --rm init`; **NU** o utilizați pentru Production. În Production realizați întâi snapshot coordonat DB+S3+chei, izolați/descărcați workerii, testați migratorul pe copia restaurată, apoi aplicați aceeași imagine OCI prin digest și execuția explicită `--migrate`.

Nu ștergeți volume/tabele pentru a forța instalarea și nu rulați downgrade SQL distructiv. Pentru rollback, promovați digestul anterior al aplicației pe schema compatibilă sau urmați recuperarea coordonată aprobată din runbook.

## Configurație PROD / Production configuration

Separate environment, database and bucket for DEV, UAT and PROD. Production source refuses SQLite and refuses unconfigured private S3 storage. Configure:

- `ASPNETCORE_ENVIRONMENT=Production`
- `Database__Provider=Postgres`, `ConnectionStrings__Database` with PostgreSQL TLS required and a dedicated restricted application role.
- `Data__Directory` durable shared directory for data-protection keys; protect and encrypt keys with an approved key-management provider. Current source only persists them to disk.
- `Storage__Provider=S3`, `Storage__Endpoint`, `Storage__Bucket`, `Storage__AccessKey`, `Storage__SecretKey`; bucket must be private, encrypted, versioned and subject to retention policy. Prefer workload identity and change the source registration if eliminating long-lived access keys.
- `Scanner__Host`, `Scanner__Port=3310` on a private service network; upload limits must also be configured at the ingress. Defaults are 25 MB/file and 200 MB/report; dated upload policy may lower/raise them up to the server cap of 100 MB/file and 1,000 MB/report.
- `Ocr__Enabled=true`, `Ocr__Languages=ron+eng`, `Ocr__MaxPages=50`; `Ocr__PdfInfo`, `Ocr__PdfRenderer`, `Ocr__Tesseract`, `Ocr__HeicConverter` may override executable paths. Poppler and libheif are installed in the image.
- SMTP TLS configuration via `Smtp__Host`, `Smtp__Port=587`, `Smtp__From`, `Smtp__Username`, `Smtp__Password`.
- `Session__Minutes=30` controls idle timeout; activity refreshes the server session. `Application__BaseUrl` must use the real HTTPS origin for recovery links.
- `ReverseProxy__KnownProxies` is **required** in Production: a comma-separated list of literal IP addresses of the ingress reverse proxies immediately in front of the app (for example `10.2.3.4,10.2.3.5`). Only those peers may supply `X-Forwarded-For` and `X-Forwarded-Proto`; the application accepts one forwarded hop. Configure the actual ingress to strip untrusted client-supplied forwarded headers and test HTTPS redirection and true client IP. Never use `*` or arbitrary public ranges.
- `DECONT_FONT_DIRECTORY` if the DejaVu font folder differs from `/usr/share/fonts/truetype/dejavu`.

Use a managed HTTPS ingress terminating TLS, enforce HSTS and trusted forwarded headers, serve frontend and API on the same origin, and restrict egress/ingress. There is no included production ingress or certificate setup. Do not enable cross-origin cookie access as a shortcut.

## Release gates

1. Restore/compile source and pass all .NET tests; remediate package/security issues using approved versions.
2. Run the reviewed numbered SQL migration through --migrate using a separate DDL identity; rehearse historical upgrades and application-image rollback on restored databases.
3. Exercise login/password change/reset/disable/logout, CSRF and cross-user authorization through the browser and real HTTP endpoints.
4. Test scanner unavailable/infected/clean flows, OCR and PDF generation with actual documents including diacritics, long content and multiple pages.
5. Finance approves dated account, tax, FX and ERP settings; compare XLSX values against company-approved expected files. Audit all overrides.
6. Test partial/multiple card allocations, settlement rounding, mismatch resolution and reapproval after a personal reimbursement increase.
7. Verify server logs, job failures, metrics, traces and alerts. `/health/live` and `/health/ready` exist; readiness currently checks the database only, not scanner/storage/SMTP.
8. Configure PostgreSQL WAL archiving/PITR, private versioned object backups and protected Data Protection key-ring backups. Run deploy/restore_drill.py --verify-existing on isolated recovered infrastructure with business-approved RPO/RTO and JSON evidence.
9. Verify scoped reporting/audit exports, accessibility, browser/mobile compatibility, performance and recovery.
10. Execute the P0 runbook on isolated HTTPS Production-mode UAT, including trusted-proxy spoof tests, common ingress rate limits, real scanner/OCR/SMTP, restarts, same-image-digest promotion and rollback; obtain separate Finance/Security/Operations sign-off.

## Retention and integrity

Posting records a retention date five years later. No business endpoint physically deletes originals. Archived lines/documents remain in the database and report packs include clean original files plus their SHA-256 manifest. Legal hold can be recorded by Finance with a reason. There is no automatic deletion job.

Audit/snapshots/decisions/reviews are protected from updates/deletes through EF SaveChanges; there are no business endpoints to modify them. Bootstrap also installs database triggers for immutable audit, snapshots, decisions, reviews and comments. Keep migration/trigger-owner privileges separate from the production runtime DB role. Export rows and the actual generated XLSX are retained with distinct hashes. Voiding an unposted export requires Finance + FinanceReopen and a reason; a posted batch can be marked Reversed only with Finance + FinanceReopen, reason and an external ERP reversal reference. Its original file/rows/reference remain retained; the report requires fresh approval before a new export. The ERP reversal itself is performed externally.

## Verificare CI / CI verification

See STATUS.md for the verified run, commit, test counts and browser evidence. Every release must rerun the SQLite/PostgreSQL suite, browser workflows and container startup. Include company ERP golden files and actual-document acceptance in UAT.

## Serviciul extern CUI / External supplier service

Optional `SupplierValidation__Endpoint` is an HTTPS company-approved adapter. The application POSTs `{country:"RO", cui, date:"yyyy-MM-dd"}`; response must contain boolean `valid` and optional `name`. `SupplierValidation__ApiKey` sets the X-API-Key header. Timeout/outage returns a visible warning; Finance can record a manual verification with reason. No service is called when the endpoint is absent. A live registry integration requires the company’s approved endpoint contract.
