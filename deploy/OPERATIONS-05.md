# Operare și lansare / Operations and release

Single-company internal deployment. Existing authentication is retained; enterprise OIDC and card ownership are out of scope by owner decision. The application does not post directly into an unspecified ERP or connect to an unspecified bank/HR provider. Approved exports and typed imports form those boundaries until actual provider specifications are supplied.

## Semnale / Signals

Admin → Background operations displays OCR, dependency and workflow counters. `/api/admin/workflow-metrics` returns pending approvals, oldest approval age, unresolved vendors/FX proposals, failed jobs, notification outbox, posting rejections and report-state counts. It contains no receipt text, CUI or employee names. Authentication and Admin authorization remain mandatory; do not expose the endpoint publicly as an unauthenticated scrape target.

Use the `Decont.Platform` meter for HTTP status counts/duration, failed sign-in attempts, committed business actions and VAT inspection counters. VAT base variance is grouped by bounded rate/document-type labels; gross inspections expose the rate combination and blocking result. Inspections are validation attempts (including repeated UI previews), not a count of unique receipts or a certified OCR accuracy measure. Committed business actions are recorded only after the transaction commits. Forward it through the deployment's approved .NET metrics collector. Audit entries carry the report ID as a stable cross-workflow correlation ID and an HTTP request ID when available. Follow the report/document event, calculation, route and export references rather than putting receipt contents in telemetry.

Configure deployment alerts for: sustained OCR failure/retry or oldest queue age, growing pending approval age, undelivered email/outbox, export failures, storage/DB dependency failures and migration failure. Thresholds and recipients belong to the company's on-call/Finance owners; counters alone do not send an external alert or prove monitoring acceptance.

## Proceduri / Runbooks

| Incident | Supported action and verification |
|---|---|
| OCR unavailable | Keep originals and clean-scan status; use manual review/entry. Admin inspects the job failure and retries supported failed jobs after recovery. Verify page coverage and avoid duplicate expenses. |
| Stuck report | Inspect state, current revision, source coverage, tasks and audit. Resolve the specific validation/assignment issue through supported UI/API. Do not update state directly in SQL. |
| Missing/absent manager | Admin repairs dated assignment/delegation; explicitly reassigns pending task with reason. No self-approval or automatic higher-manager fallback. |
| Bad FX import | Stop further use, preserve import/source and decision references, have Finance record a corrected dated rate/decision and reason. Exported history remains frozen; do not rewrite it. |
| VAT/mapping issue | Resolve through controlled dated configuration and document the authority. Tax-policy maker/checker remains distinct from ordinary GL/ERP editing. Revalidate source totals and preview. |
| Export incident | Inspect batch/hash/report revision. Retry the same business operation safely; reconcile with ERP owner before reissue. Posted-batch reopening requires the existing controlled reversal/reference flow. |
| Access/privacy incident | Disable/revoke affected sessions, preserve audit, restrict access through roles, notify the designated security/privacy owner. Do not include receipts in ordinary incident logs. |
| Storage restored | Verify database metadata, original-object hashes, private access policy and data-protection keys together. Never assume a DB-only restore recovers receipts. |
| Migration failure | Keep writes paused. Inspect schema release and backup; fix the documented issue (including duplicate LantID) in a reviewed change. Repeat init only for this application's DB. Restore the coordinated backup if rollback is chosen. |

## Restaurare / Recovery rehearsal

1. Operations approves RPO/RTO and selects an isolated staging restore target. Record release SHA and a consistent recovery point.
2. Pause application writes and workers. Export PostgreSQL using approved backup/PITR tooling; export/version-lock original objects and protect data-protection keys. Preserve object keys, hashes and backup checksums. Record completion times without credentials.
3. Restore into an empty isolated database and private object location using the same application image/schema. Keep outbound email/ERP integrations disabled.
4. Verify schema version, report/revision/route/task counts, audit/export fingerprints, original-object SHA-256 and authenticated source access. Exercise login, report resume, pending approval, OCR/manual fallback and historical export download.
5. Record actual data loss/recovery duration against RPO/RTO. Reconcile Finance/export state with external ERP before resuming writes. Obtain Operations/Finance acceptance; a written procedure is not a completed drill.

Existing deployment configuration covers local evaluation. Production requires approved hosting, region, private encrypted PostgreSQL/object storage, HTTPS ingress, secret/key management, environment isolation and monitored backup policies. Use provider-specific infrastructure reviewed by Operations; do not copy local development credentials or declare loopback Compose a production environment.

## Încărcare / Pilot performance

Use `node deploy/probe-reads.mjs` only against an authorized staging/pilot account, supplying `PROBE_ORIGIN`, `PROBE_USERNAME`, `PROBE_PASSWORD`; optionally `PROBE_READS` and `PROBE_CONCURRENCY`. Credentials are not printed. The result measures authenticated report reads on that dataset; it does not prove production availability or OCR throughput. Proposed ordinary-read p95 is 400 ms and availability 99.9%, pending owner acceptance and realistic measurements.

## Acceptanță / Acceptance

Copy `release-evidence.example.json` into the private release workspace. Add actual build/test evidence, Finance golden-case approval, HR organisation approval, ERP test-import result, privacy/security review, accessibility/UAT, measured load, coordinated restore, monitoring/on-call and training/cutover evidence. Operations defines RPO/RTO; the business owner signs the actual release decision.

`node deploy/check-release.mjs path/to/release-evidence.json` fails when required references/owners/dates are missing. It checks completeness only: a reference or claimed signer must still be independently verified. The shipped example intentionally fails and is not a release approval.

Lansarea se face controlat, după acceptanță; se păstrează imaginea și backupul anterior, se verifică indicatorii și se urmăresc excepțiile zilnic în pilot. / Launch only after real acceptance, retain the prior image/coordinated backup, monitor the limited rollout and review exceptions daily during the pilot.

CI generates a CycloneDX 1.6 inventory from the actual resolved NuGet and npm dependency graphs. Inventory is separate from vulnerability assessment; existing build/analyzer, dependency-audit, API/security and browser gates remain required. When GitHub artifact storage is unavailable, validation continues and the browser job rebuilds the same commit instead of substituting a mock application. Transfer errors remain visible; generated screenshots/reports may not be retained until the account quota is resolved.
