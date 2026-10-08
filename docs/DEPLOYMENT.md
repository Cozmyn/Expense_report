# Implementare / Deployment

## Local / DEV

`compose.yaml` is a local evaluation setup bound to loopback. It uses PostgreSQL, private local files and ClamAV. It sets Development mode to allow local HTTP. Do not expose this Compose configuration publicly or use its local generated admin email/password in production. Docker image build executes tests and stops on failure.

Node 22 builds the frontend. .NET 10 builds/publishes the backend. Runtime image uses the official `10.0-noble` Ubuntu variant and installs Romanian/English Tesseract, Poppler and DejaVu Unicode fonts. Image tags are mutable: pin approved digests in the release pipeline. Bootstrap creates new installations and applies the application’s repeatable additive schema upgrades. Do not point `--init` at an existing unrelated database.

## Actualizarea unei instalări existente / Existing installation upgrade

Pentru instalările acestei aplicații anterioare fluxului de bonuri, realizează backup și oprește versiunea veche; construiește noua imagine, apoi rulează serviciul de inițializare înainte de pornirea aplicației:

```sh
docker compose build
docker compose --profile setup run --rm init
docker compose up -d web
```

`--init` / `--bootstrap` creează repetabil tabelele de atașamente, preferințe, comentarii, alocări și versiuni de schemă, plus coloanele noi și triggerele append-only, dacă lipsesc. Nu rescrie deconturile, bonurile sau documentele existente. Câmpurile noi ale bonului sunt stocate în JSON; documentele vechi cu `InvoiceWithCui` păstrează interpretarea CUI/TVA și moneda contabilă RON până la editare. Acest upgrade aditiv este testat pe SQLite/PostgreSQL; nu înlocuiește un sistem complet de migrări și rollback pentru producție.

**English:** Back up the application's existing database, stop the old application, build the new image, run the setup service, then start the application. The additive attachment-table upgrade is idempotent and preserves existing reports. Do not run it against an unrelated database.

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
- `DECONT_FONT_DIRECTORY` if the DejaVu font folder differs from `/usr/share/fonts/truetype/dejavu`.

Use a managed HTTPS ingress terminating TLS, enforce HSTS and trusted forwarded headers, serve frontend and API on the same origin, and restrict egress/ingress. There is no included production ingress or certificate setup. Do not enable cross-origin cookie access as a shortcut.

## Release gates

1. Restore/compile source and pass all .NET tests; remediate package/security issues using approved versions.
2. Create and commit an initial EF migration and upgrade/rollback strategy. Current EnsureCreated bootstrap is not a production migration system.
3. Exercise login/password change/reset/disable/logout, CSRF and cross-user authorization through the browser and real HTTP endpoints.
4. Test scanner unavailable/infected/clean flows, OCR and PDF generation with actual documents including diacritics, long content and multiple pages.
5. Finance approves dated account, tax, FX and ERP settings; compare XLSX values against company-approved expected files. Audit all overrides.
6. Test partial/multiple card allocations, settlement rounding, mismatch resolution and reapproval after a personal reimbursement increase.
7. Verify server logs, job failures, metrics, traces and alerts. `/health/live` and `/health/ready` exist; readiness currently checks the database only, not scanner/storage/SMTP.
8. Configure PostgreSQL backups with WAL archiving/PITR, object versioned backups and data-protection-key backups. Perform a timed isolated restore; the RPO/RTO targets are not verified by providing this source.
9. Verify scoped reporting/audit exports, accessibility, browser/mobile compatibility, performance and recovery.
10. UAT sign-off and a rollback rehearsal before a production deployment.

## Retention and integrity

Posting records a retention date five years later. No business endpoint physically deletes originals. Archived lines/documents remain in the database and report packs include clean original files plus their SHA-256 manifest. Legal hold can be recorded by Finance with a reason. There is no automatic deletion job.

Audit/snapshots/decisions/reviews are protected from updates/deletes through EF SaveChanges; there are no business endpoints to modify them. Bootstrap also installs database triggers for immutable audit, snapshots, decisions, reviews and comments. Keep migration/trigger-owner privileges separate from the production runtime DB role. Export rows and the actual generated XLSX are retained with distinct hashes. Voiding an unposted export requires Finance + FinanceReopen and a reason; a posted batch can be marked Reversed only with Finance + FinanceReopen, reason and an external ERP reversal reference. Its original file/rows/reference remain retained; the report requires fresh approval before a new export. The ERP reversal itself is performed externally.

## Verificare CI / CI verification

See STATUS.md for the verified run, commit, test counts and browser evidence. Every release must rerun the SQLite/PostgreSQL suite, browser workflows and container startup. Include company ERP golden files and actual-document acceptance in UAT.

## Serviciul extern CUI / External supplier service

Optional `SupplierValidation__Endpoint` is an HTTPS company-approved adapter. The application POSTs `{country:"RO", cui, date:"yyyy-MM-dd"}`; response must contain boolean `valid` and optional `name`. `SupplierValidation__ApiKey` sets the X-API-Key header. Timeout/outage returns a visible warning; Finance can record a manual verification with reason. No service is called when the endpoint is absent. A live registry integration requires the company’s approved endpoint contract.
