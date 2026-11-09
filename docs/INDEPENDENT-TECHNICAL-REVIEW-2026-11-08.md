# Independent review of Decont_Expense_Report_Full_Review.pdf

**Review date:** 2026-11-08. **Reference:** `main` @ `5b2f93348b31c9311ecc2757ce44dbbd5c59a3e6`.
**Scope:** independent static verification of selected findings and critical paths, compared with the 26-page, 50-finding external review. This is NOT a comprehensive security audit, a production certification, or an executed local test suite. GitHub Actions on main is green: [run 42290981224](https://github.com/Cozmyn/Expense_report/actions/runs/42290981224); green CI is not proof of real ERP/OCR acceptance.

## Summary of decisions

**Accept as engineering priorities (without authorising behaviour changes):**
- BE-01: frontend and backend formatting/readability. Observed `server/Api.cs` ~13.7 KB/38 lines, `server/FinancialCalculations.cs` ~25.8 KB/64 lines, `ui/src/main.tsx` ~69 KB/66 lines, `ui/src/style.css` ~27 KB/50 lines. Some lines are **longer than the report's suggested ~1,800-character maximum**; the CSS contains a line around 15,900 characters. Formatting should be mechanical and separate, followed by checks for unintended edits.
- DB-01: `Program.cs` uses `EnsureCreatedAsync` and `SchemaUpgrades.Apply`; `SchemaUpgrades` uses regex over generated DDL and handcrafted column/trigger operations. This is a significant upgrade/restore risk. **Do not delete, replace or execute schema changes until an existing-database migration and rollback plan is proven.**
- BE-02: `Reports.Validate` queries all unarchived Lines and Evidence, and loads Masters within a line loop; `Api.cs` invokes validation when fetching report details. Confirm performance cost through profiling before changing behaviour. Duplicate visibility, archived records, finance flags and validation outcomes must be preserved until owner approves a different rule.
- BE-03: report list loads all scoped reports with Lines and Attachments before mapping. A paginated API changes consumers; prove contract compatibility first.
- FE-01: `main.tsx` is monolithic and uses broad dynamic state. Extract incrementally only after characterization tests.
- SEC-01/SEC-02: `compose.yaml` uses Development; `Program.cs` has an IP-partitioned login limiter and no explicit forwarded-headers setup. The actual reverse-proxy deployment is unknown. Treat as deployment-configuration risks; do not assume that the local dev stack is production.
- FE-04: `ui/src/api.ts` has no explicit 401/428 session-expiration handling; recovery must not lose unsaved drafts.
- QA-01: real OCR image accuracy, load/recovery tests and live ERP acceptance are not demonstrated. `OcrCorpusTests` explicitly calls itself `synthetic-parser-v1` and `businessApproved=false`, not real OCR measurement.

**Needs deeper verification before promoting to an implementation ticket:**
- SEC-05: `Reports.Action` finance completion and `Reports.Edit` need a complete role x state x report-owner permissions matrix; admin and exceptional roles must be understood before setting a universal NotOwnReport rule.
- DB-04: check actual EF indexes, generated PostgreSQL DDL and query plans before adding indexes blindly.
- DB-06: effective dates often derive from UTC. Distinguish UTC instant timestamps from Romanian business dates; do not automatically change any historically effective date or API format.
- BE-07: test serialization/deadlock behaviour and idempotency before adding retries: retries must not duplicate side effects or email/export work.
- UX-04: post-approval modification/reapproval requires Finance and owner policy decision, not an assumed defect.
- SEC-07/SEC-08: uploads are buffered and conversion processes run in worker paths; confirm resource profiles and threat model before introducing a new storage/worker contract.
- SEC-03: CSP rollout needs checks for legitimate camera, document and preview functionality; report-only first.
- OPS-03/OPS-04: production observability and hosting require agreed topology and access to a target environment.

**Reject or defer the report's proposed solution as written:**
- DB-03: no evidence justifies **deleting SQLite support**; currently valuable for development and independent testing.
- DB-07: gap-free numbers are not guaranteed by PostgreSQL sequences after rollbacks; report-number changes are a product/accounting decision, with backward compatibility constraints.
- DB-09: EF global Archived filters can accidentally hide legitimate immutable evidence/audit views. Prefer query-specific tests before introducing broad global filters.
- BE-05: caching roles 30-60 seconds can delay user deactivation or privilege revocation. Preserve immediate revocation until an explicit security decision.
- SEC-02 sample at PDF p21: using a client-controlled `X-Login-Hint` in a limiter key creates an evasion opportunity. Use server-verified, normalized identifiers and multiple throttling controls if redesigned.
- DB-01 sample at PDF p19: manually inserting a hardcoded migration history row is **not** a safe universal baseline procedure; it must match the actual migration identifiers, provider, schema and applied database state. Do not run it.
- DB-02 sample at PDF p19: `gross_amount <> 0`, `supplier_cui NOT NULL`, and the proposed relational schema could exclude valid historical or special-case records. No data migration without mapping every existing branch and test.
- UX-03: excluding other employees' drafts/rejected reports from duplicate detection changes anti-fraud behaviour; owner decision necessary.
- UX-04: adding new approval triggers or automatic accept-all rules changes the workflow; owner decision necessary.
- FE-02/FE-03/FE-05/FE-07: wholesale router, query library, localization framework or design-system replacement should not be implemented merely for architectural preference. Measure impact and migrate one feature at a time.
- OPS-06: do **not** delete/archive/rewrite existing delivery documentation or rename tests without approval; archival moves are deletions at previous paths.
- UX-05: proposed new policy engine, ERP API, per diems, or approval tiers are NEW features, not technical corrections.

## Independent risks that deserve explicit attention

1. **Immutability scope:** EF `SaveChangesAsync` blocks updates/deletes to many evidence types. Verify database triggers and backup/operator privileges separately; immutability in the ORM is not universal database immutability.
2. **Financial acceptance:** exact-decimal plumbing and passing synthetic unit tests are valuable, but cannot establish accounting correctness with a real, signed-off golden workbook, mixed VAT, currency, deductibility, reversals, and posting exceptions.
3. **Database rollback and disaster recovery:** test from a seeded historical database, include immutable triggers and application secrets/Data Protection keys in restore rehearsals, and define RPO/RTO before migrating.
4. **Release identity:** the main branch is `5b2f933`. A more recent passing GitHub Actions run on another branch is not evidence that those commits exist on main.
5. **External review limitations:** its own scope statement says no build or UX inspection, no penetration/load testing, and many finance/OCR modules skimmed. Scoring and estimates therefore should be treated as provisional.

## 50-finding decision register

- **A** = accepted as an investigation or safe hygiene proposal; not blanket approval to change code.
- **V** = verify with tests, business owner or deployment information before implementing.
- **D** = defer the particular proposed fix; preserve the current feature or contract.

| Finding | Decision | What must be protected / evidence required |
| --- | --- | --- |
| SEC-01 | V | Dev compose is not a production reference; confirm hosting before new prod config |
| SEC-02 | V | Proxy identity and login throttling; reject client-controlled hint example |
| SEC-03 | V | CSP report-only and camera/document preview acceptance |
| SEC-04 | D | SSO/MFA requires confirmed company IdP and user lifecycle policy |
| SEC-05 | V | Full permissions matrix including employees, Finance, managers, delegates and admin |
| SEC-06 | V | Timing and audit testing while protecting login privacy and reset policies |
| SEC-07 | V | Upload limits and streaming without breaking multi-document intake |
| SEC-08 | D | Separate parser worker architecture only after threat model and integration plan |
| SEC-09 | V | Reset token handling; preserve working one-time reset flow |
| SEC-10 | V | Secrets strategy belongs to known production infrastructure |
| DB-01 | V | Existing-database snapshots, baseline, rollback, triggers; NO deletion |
| DB-02 | D | No relational/JSON backfill without complete mapping and golden cases |
| DB-03 | D | Preserve SQLite support |
| DB-04 | A | Measure query plans and indexes before proposing migration |
| DB-05 | V | Enumerate all valid existing states, API clients and transitions first |
| DB-06 | V | Distinguish UTC instant from Romanian policy effective day |
| DB-07 | D | Preserve numbering; sequence is not gap-free |
| DB-08 | V | Backups, restore, retention and GDPR depend on deployment/legal decisions |
| DB-09 | D | No global archived filter without evidence/audit-view proof |
| BE-01 | A | Mechanical-only formatter; separate commit and CI when tested |
| BE-02 | A | Characterize all duplicate/validation cases before query refactor |
| BE-03 | V | Preserve existing unpaginated API consumers unless agreed otherwise |
| BE-04 | A | Add `AsNoTracking` only to proven read-only paths; avoid tracking regressions |
| BE-05 | D | No delayed role revocation or blind purge of evidence |
| BE-06 | V | Incremental feature boundaries, not a big-bang rewrite |
| BE-07 | V | Transaction retries require safe side-effect/idempotency tests |
| BE-08 | V | API schema and typed client can be additive; do not change errors automatically |
| BE-09 | D | No worker redeployment before host and queue lifecycle decisions |
| FE-01 | A | Characterization tests before extracting React code |
| FE-02 | V | Routing/deep links alter URL/session/navigation behaviour |
| FE-03 | D | Avoid wholesale fetching-library swap without measured need |
| FE-04 | V | Preserve draft state on session expiry; test 401/428 |
| FE-05 | D | Existing RO/EN wording and meanings must be preserved |
| FE-06 | A | Add component/a11y tests without changing screens |
| FE-07 | D | No UI kit migration or styling changes without design approval |
| FE-08 | V | Measure bundle size before code splitting |
| UX-01 | V | User research, not a unilateral capture-flow redesign |
| UX-02 | V | Deep links/templates require routing and privacy review |
| UX-03 | D | Duplicate rules are financial controls; owner approval required |
| UX-04 | D | Reapproval triggers and Finance FX acceptance are business rules |
| UX-05 | D | New requested capabilities are features, not tech-debt fixes |
| UX-06 | A | Audit and tests first; UI changes separately approved |
| OPS-01 | V | Don't remove safety tests from Docker until CI/release gates equivalently protect |
| OPS-02 | A | Additive security analysis/renovation proposals after license and risk review |
| OPS-03 | V | Target observability architecture depends on production environment |
| OPS-04 | D | Cannot invent hosting architecture and service-level requirements |
| OPS-05 | V | Confirm scanner readiness behaviour and visible pending states first |
| OPS-06 | D | Keep historic documents and test files; add index rather than delete |
| QA-01 | A | Add load, property and recovery evidence, no financial logic changes |
| QA-02 | D | Avoid file moves/renames without consent |

## Implementation status in this branch

- Added `.editorconfig` and `.prettierrc.json` as **non-enforcing** editor/formatter configuration. They do not reformat existing code and are not wired into CI.
- Added `docs/SAFE-CHANGE-PROTOCOL.md` to codify explicit owner approval for any changed behaviour, deleted content or schema migration.
- Added this independent review document for traceability.
- **NOT done:** formatter pass, `dotnet format`, Prettier rewrite, CI enforcement, migrations, file deletion, permission/approval/duplicate rule changes, refactor, deployment changes. Runtime tests are not claimed as executed for the added documentation/configuration.

## Next safe implementation milestones (separate PRs)

1. Capture and validate the exact baseline on the current main branch: tests, API snapshots, ERP 24-column fixtures and Finance/OCR golden cases.
2. Mechanical formatting in a standalone PR, with token-sensitive checks and CI on both DBs; do not change business logic.
3. Read-only performance profiling and regression tests on `Reports.Validate` and report list before proposing code changes.
4. Bring owner decisions for reapproval, SoD, duplicate scope and hosting as written proposals, not executable changes.

**Owner approval is required before any logic change, migration, deletion, API change or `main` merge.**
