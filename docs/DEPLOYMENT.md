# Implementare / Deployment

## Local / DEV

`compose.yaml` is a local evaluation setup bound to loopback. It uses PostgreSQL, private local files and ClamAV. It sets Development mode to allow local HTTP. Do not expose this Compose configuration publicly or use its local generated admin email/password in production. Docker image build executes tests and stops on failure.

Node 22 builds the frontend. .NET 10 builds/publishes the backend. Runtime image uses the official `10.0-noble` Ubuntu variant and installs Romanian/English Tesseract, Poppler and DejaVu Unicode fonts. Image tags are mutable: pin approved digests in the release pipeline. The initial schema command is for a new database only. Do not point `--init` at an existing unrelated database.

## Configurație PROD / Production configuration

Separate environment, database and bucket for DEV, UAT and PROD. Production source refuses SQLite and refuses unconfigured private S3 storage. Configure:

- `ASPNETCORE_ENVIRONMENT=Production`
- `Database__Provider=Postgres`, `ConnectionStrings__Database` with PostgreSQL TLS required and a dedicated restricted application role.
- `Data__Directory` durable shared directory for data-protection keys; protect and encrypt keys with an approved key-management provider. Current source only persists them to disk.
- `Storage__Provider=S3`, `Storage__Endpoint`, `Storage__Bucket`, `Storage__AccessKey`, `Storage__SecretKey`; bucket must be private, encrypted, versioned and subject to retention policy. Prefer workload identity and change the source registration if eliminating long-lived access keys.
- `Scanner__Host`, `Scanner__Port=3310` on a private service network; upload limits must also be configured at the ingress. The current code imposes 25 MB/file and 200 MB/report.
- `Ocr__Enabled=true`, `Ocr__Languages=ron+eng`; PDF renderer/Tesseract executable settings can be overridden.
- SMTP TLS configuration via `Smtp__Host`, `Smtp__Port=587`, `Smtp__From`, `Smtp__Username`, `Smtp__Password`.
- `Session__Minutes=30` currently means absolute lifetime, not idle timeout.
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

Audit/snapshots/decisions/reviews are protected from updates/deletes through EF SaveChanges; there are no business endpoints to modify them. Production DB grants/triggers should enforce append-only permissions independently of the application. Export rows and the actual generated XLSX are retained with distinct hashes. Voiding an unposted export requires Finance + FinanceReopen and a reason; posted export reversal is an external ERP procedure not automated here.
