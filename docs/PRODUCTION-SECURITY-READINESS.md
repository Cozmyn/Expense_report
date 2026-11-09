# Stage 4 - security and production readiness (not a production release)

Reference implementation: `main` at `5b2f933`, plus the separately reviewed formatting branch `refactor/mechanical-format-20261108`.
Scope: evidence-gathering, security regression tests and **read-only** deployment preflight. These additions do not amend security policy, change business behavior, or perform migrations.

## What is already implemented and what is actually verified

| Control | Evidence in the application | What is not proven |
| --- | --- | --- |
| Server-side sessions | `Tickets` persists protected authentication tickets in `Sessions`; logout and user changes revoke sessions | Production key lifecycle, multi-instance configuration and recovery |
| Cookies and CSRF | `Program.cs` sets HttpOnly, SameSite Strict, Secure Always outside Development; antiforgery validates API writes | Real ingress TLS/proxy behavior, CSRF across approved infrastructure |
| Roles and object scope | Role groups in `Admin` / `Finance`; report access via `Reports.Scope/Get` | Complete organization-specific separation-of-duties policy and penetration test |
| Idempotency and stale writes | `Api.Atomic` requires a key, serializable transaction and version checks | Exhaustive concurrent/external side-effect testing |
| Antimalware | ClamAV scanning plus quarantine for uploaded originals | Production scanner availability, failure recovery, updated signatures and file corpus |
| Audit and retention | Protected records/DB triggers, revisions and evidence retention | Operator privilege separation, independently verified backup and restore |
| Build verification | Existing CI tests SQLite and PostgreSQL, Playwright and container | Company ERP acceptance, real-document OCR accuracy, load and recovery |

**New security regression tests** in `tests/SecurityBoundaryRegressionTests.cs` cover anonymous and cross-user authorization, csrf/missing key rejections, separate Finance/Admin scopes, session isolation, cookie flags and HTTP defensive headers. They are regular xUnit tests, run on both databases by existing CI.

## Production deployment blockers / no-go items

1. **Ingress / proxy identity:** `Program.cs` has `UseHttpsRedirection` and HSTS in non-development mode but does not configure `UseForwardedHeaders` with a trusted-proxy allowlist. Current IP-based login limiter sees the connection peer, which may be the proxy. Do **not** simply trust arbitrary `X-Forwarded-For` headers or client-controlled login hints. Require a reviewed proxy design and integration tests before changing runtime code.
2. **Development Compose is NOT production Compose:** `compose.yaml` explicitly uses Development and binds HTTP to loopback. This is for local evaluation, not an authenticated production exposure. Never copy it unchanged to a public server.
3. **Keys, private storage and secrets:** Production code requires PostgreSQL and S3; Data Protection keys persist to the data directory. Verify durable encrypted key storage, application-role DB privileges, private S3/IAM and bucket versioning, TLS, secret rotation and backups.
4. **DB upgrades:** `EnsureCreatedAsync` plus `SchemaUpgrades` is not a proven reversible migration pipeline. Do not delete or replace either mechanism or run against real data without snapshots, repeatability tests and owner approval.
5. **Operations and recovery:** `/health/ready` currently checks DB connectivity only; it is not an end-to-end scanner, SMTP or S3 readiness guarantee. Create and rehearse alerts, runbooks, restore tests, rollback and RPO/RTO on the approved infrastructure.
6. **Company acceptance:** Finance must approve the tax/FX policies, approved golden ERP workbook, actual document intake/corpus and the complete role-by-state authorization matrix. No synthetic test constitutes this acceptance.

## Read-only configuration preflight

Run only against a controlled environment with secrets already supplied through the deployment secret provider:

```sh
python3 deploy/production_preflight.py
# For a machine-readable REDACTED summary:
python3 deploy/production_preflight.py --json
```

The preflight:
- Reads **only existing environment variables**; it does not read `.env` files, connect to anything, provision resources, modify files or reveal credential values.
- Returns **1** for a demonstrably missing/insecure local setting (wrong environment/provider, plaintext public origin/object endpoint, missing bucket/credentials/scanner/key directory or invalid session timeout); otherwise **0** with a list of mandatory manual reviews.
- Always reports trusted-proxy validation, backup/restore, production capacity/alerts, and ERP/OCR acceptance as **manual blockers for go-live**, even if the script exits successfully.
- Checks configuration consistency, **not** certificate validity, network accessibility, secret permissions, data correctness, or production readiness.

To run its no-network tests:

```sh
python3 -m unittest discover -s deploy -p 'test_production_preflight.py' -v
```

The project CI remains the source of execution evidence for the HTTP regression tests. Do not enter real secrets into pull-request logs, test fixtures, public issues or review comments.

## Production acceptance gate (requires actual environment and responsible owners)

- [ ] Environment and identity architecture approved (TLS, trusted proxy list, origin, MFA/SSO decision, joiner/mover/leaver procedure).
- [ ] Role-by-state-by-object access matrix approved by Finance and system owner; denied actions documented.
- [ ] Production PostgreSQL TLS, least-privilege role, backups/PITR and tested restore from a consistent snapshot.
- [ ] Private S3 with encryption, lifecycle/retention, object restore, private scanning service and deterministic scanner-down behavior.
- [ ] Durable encrypted Data Protection keys; app roll/restart and disaster recovery verified without silently invalidating sessions.
- [ ] SMTP password recovery tested end to end with approved real email gateway.
- [ ] Staging tests: cross-employee IDOR, CSRF, forwarded headers/IP rate limits, 401/403/428, session expiry, role revocation, upload restrictions.
- [ ] Finance-approved VAT/FX samples and ERP 24-column golden files; representative Romanian documents/OCR.
- [ ] Load/capacity, uptime/availability, monitoring and alerts with incident response ownership.
- [ ] Recovery and rollback rehearsal with agreed RPO/RTO, plus explicit written owner sign-off.

## Decisions not implemented

No silent change to login limiting, role revocation, Finance roles, admin permissions, CSRF behavior, session timeout, login/SSO, CSP, proxy handling, schemas, uploads, OCR processing, retention or historical documents. Each requires a threat model, compatibility review, tests and explicit approval before a behavior-changing change. **Do not merge or deploy this review on the assumption that production is certified.**
