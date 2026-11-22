# PLT-204 → PLT-209 — Full-Project QA Report

Date: 2026-11-21 · Configuration: **Release** · Environment: Windows, .NET 10 SDK, no PostgreSQL, no Docker.
Scope: every solution project, all discoverable test projects, plus the JS apps.

> Verification policy applied here: a project is called "passed" only where its test run reported
> `Failed: 0` and exited 0. Skipped tests are listed, never counted as passed. Nothing is marked
> verified because the code compiles.

---

## 1. Complete test inventory

### .NET test projects (6 — the only ones that exist)

| # | Project | Path | Frameworks/target | Kind |
|---|---|---|---|---|
| 1 | Construction.Kernel.Tests | `backend/tests/Shared/Construction.Kernel.Tests` | net10.0, xUnit | unit |
| 2 | Construction.Modules.Tests | `backend/tests/Shared/Construction.Modules.Tests` | net10.0, xUnit | unit |
| 3 | Construction.Api.Tests | `backend/tests/Host/Construction.Api.Tests` | net10.0, xUnit | host/API |
| 4 | Construction.Platform.Domain.Tests | `backend/tests/Modules/Platform/Platform.Domain.Tests` | net10.0, xUnit | domain + contract + a few Postgres (`D2_TEST_PG`) |
| 5 | Construction.Persistence.Tests | `backend/tests/Shared/Construction.Persistence.Tests` | net10.0, xUnit | unit + Docker PG (`INTEGRATION_TESTS_ENABLED`) + live PG (`LIVE_PG_ENABLED`) |
| 6 | Construction.Web.Tests | `backend/tests/Shared/Construction.Web.Tests` | net10.0, xUnit | unit + in-process host (TestServer) |

Found by `find . -name "*.csproj" | grep -Ei "test|spec"`; the solution has 23 projects total
(6 test + 17 production: 2 hosts, 5 shared libraries, 4 Platform projects, 5 module-template projects,
1 additional module project).

### Directories that look like test suites but contain no tests

These exist only with `.gitkeep` placeholders — **no project, no sources, nothing to run**:
`backend/tests/Architecture.Tests`, `Contract.Tests`, `E2E`, `GoldenProject.Tests`, `Load`,
`Tenant.Isolation.Tests`, and `Modules/{Cash,Closeout,Contracts,Core,Cost,Docs,Estimating,Intelligence,Planning,Procurement,Reporting,Sourcing,Subcontracts}/*.{Domain,Integration}.Tests`
(28 empty folders), plus `Modules/Platform/Platform.Integration.Tests`.

### Non-.NET suites

`apps/web` and `apps/mobile` have **no test framework, no test files** (`find` for `*.test.*`/`*.spec.*` → 0)
and no test script in `package.json` (`apps/mobile` exposes only `typecheck`/`lint`; `apps/web` only
`dev/build/start/lint`). `node_modules` is not installed. Nothing was run there.

---

## 2. Exact results

### Build — full solution, Release, warnings as errors

```
dotnet build backend/Construction.sln -c Release -warnaserror -m:1 -nodeReuse:false --no-restore
→ exit 0 · Build succeeded · 0 errors
```
(Debug build with the same flags also succeeds.)

### Tests — whole solution, Release

```
dotnet test backend/Construction.sln -c Release --no-build -m:1 -nodeReuse:false
→ exit 0
```

| Project | Passed | Failed | Skipped | Total | Result |
|---|---:|---:|---:|---:|---|
| Construction.Kernel.Tests | 63 | 0 | 0 | 63 | passed |
| Construction.Modules.Tests | 8 | 0 | 0 | 8 | passed |
| Construction.Api.Tests | 10 | 0 | 0 | 10 | passed |
| Construction.Platform.Domain.Tests | 212 | 0 | 8 | 220 | passed |
| Construction.Persistence.Tests | 54 | 0 | 40 | 94 | passed |
| Construction.Web.Tests | 130 | 0 | 0 | 130 | passed |
| **Total** | **477** | **0** | **48** | **525** | **passed** |

Skipped, with reasons supplied by the tests themselves:
- Platform (8): `DeadLetterPostgresTests` — `D2_TEST_PG` connection string not set.
- Persistence (8): `LivePostgreSqlTenantIsolationTests` — `LIVE_PG_ENABLED` not set.
- Persistence (32): `PostgreSqlIntegrationTests`, `PostgreSqlKeysetPaginationTests`,
  `PostgreSqlTenantIsolationTests`, `PostgreSqlConcurrencyRecoveryTests` — `INTEGRATION_TESTS_ENABLED`
  not set (requires Docker, which is not installed).

No test was skipped as a workaround for a failure; all 48 skips are environment opt-outs that
predate this change (the attribute message is quoted above).

### Focused runs used for the ticket evidence

```
dotnet test .../Platform.Domain.Tests --filter "FullyQualifiedName~NumberSeriesRulesTests|...~FilePolicyTests|...~ApprovalPolicyMatcherTests|...~NotificationRulesTests|...~PlatformErrorCodesTests|...~DeadLetterSingleConsumerGuardTests"
→ Failed: 0, Passed: 133

dotnet test .../Construction.Web.Tests --filter "FullyQualifiedName~Idempotency"
→ Failed: 0, Passed: 20
```

---

## 3. All build and test errors observed

| # | Run | Error | Classification |
|---|---|---|---|
| 1 | First `dotnet test Construction.sln` (Debug) | `MSB4166 System.OutOfMemoryException` → `Bus error (exit 138)` | environment (disk was 100 % full, now 9.4 GB free) |
| 2 | `Platform.Infrastructure` build | `CS1061 MigrationBuilder does not contain EnableTenantRowLevelSecurity` ×6 migrations | real compile error — fixed |
| 3 | `PostgresFileService` | `CS0411 type arguments for Fail<T> cannot be inferred` ×25 | real compile error — fixed |
| 4 | `PostgresApprovalService` | `CS8604 possible null for 'value'` | real compile error — fixed |
| 5 | `PostgresApprovalService` / `PostgresNotificationService` | `CA1822` ×3, `CA1859` ×1 (warnings as errors) | real analyzer errors — fixed |
| 6 | New test files | `CS0246 Fact/Theory/InlineData not found` | missing `using Xunit;` — fixed |
| 7 | `NotificationRulesTests` | `Render_leaves_unknown_placeholders_empty…` failed (`"ok {{known.toUpper()}}"`) | test expectation wrong — test corrected to assert inert output |
| 8 | `NumberSeriesRulesTests` | lock-validation case passed `start: 1` (unchanged value) so no error | bad test data — corrected to `5` |
| 9 | `RequestHasherTests.Hash_normalises_equivalent_numbers` | hashes differed for `1` vs `1.0` | **real product defect** — decimal scale was preserved; fixed canonicalisation |
| 10 | `InMemoryIdempotencyStoreTests.Purge_removes_only_expired_rows` | `Assert.NotNull` failed after purge | test bug: helper generated a new tenant per call |
| 11 | Whole-solution Release run | `Test Run Failed` for Persistence with **0 failed tests**: `NpgsqlException: Failed to connect to 127.0.0.1:5432` in `LivePostgreSqlFixture.DisposeAsync` | **real test-infrastructure defect** — fixture connected even when all its tests were skipped; fixed |

---

## 4. Issues fixed

1. **Idempotency body hashing treated `1` and `1.0` as different requests.** `decimal.ToString()`
   preserves the parsed scale, so semantically identical bodies produced different hashes and a
   legitimate retry would have been rejected as key reuse. Now formatted with
   `"0.############################"`; re-run of the 20 idempotency tests is green.
2. **Live PostgreSQL fixture broke the test run when the DB tests were disabled.**
   `InitializeAsync`/`DisposeAsync` now return immediately unless `LIVE_PG_ENABLED=true`, matching the
   `LiveDatabaseFact` skip attribute. The whole-solution run went from `exit 1` (0 test failures) to
   `exit 0`.
3. Compile/analyzer errors 2–6 above (migration extension imports, explicit `Result.Failure<T>`,
   nullable parameter, static members, collection return type, missing `using Xunit`).
4. Test-data corrections 7–10 (no product behaviour weakened; assertions kept or strengthened —
   the placeholder test gained an extra case asserting expression syntax is never evaluated).

---

## 5. What could not be tested, and why

| Area | Reason | Evidence needed to close |
|---|---|---|
| PLT-204 Postgres dead-letter replay/atomicity/RLS (`DeadLetterPostgresTests`, 8) | no PostgreSQL server | `D2_TEST_PG` + `dotnet test … --filter DeadLetterPostgresTests` |
| PLT-205 Postgres idempotency claim under concurrency | same | Postgres store integration test (`ON CONFLICT DO NOTHING` single-winner) |
| PLT-206 allocation concurrency / advisory lock / append-only trigger | same | two-connection allocation race test |
| PLT-207 upload→scan→quarantine→download, presign | no S3/MinIO, no scanner | provider-backed contract test |
| PLT-208 decision versioning + append-only trigger under concurrency | same | two-connection approve race test |
| PLT-209 email delivery/retry/dead-state against a provider | no SMTP/provider | provider sandbox test; semantics are at-least-once |
| Persistence Docker + live RLS suites (40) | Docker absent, `INTEGRATION_TESTS_ENABLED`/`LIVE_PG_ENABLED` unset | Docker Desktop + `INTEGRATION_TESTS_ENABLED=true`; local PG + `LIVE_PG_ENABLED=true` |
| `Tenant.Isolation.Tests`, `E2E`, `Contract.Tests`, `GoldenProject.Tests`, `Load`, `Architecture.Tests` | **no project or source files exist** (only `.gitkeep`) | authoring the suites — cannot be run at any time |
| All business modules except Platform (Cash, Cost, Estimating, Procurement, …) | they are **scaffolds**: no `.csproj`, no source, so no code and no tests exist to run | module implementation tickets |
| Construction.Worker (background host) | compiles in Release; **no test project exists**; jobs are covered indirectly by Platform job tests (16 schedule + runner tests) | worker host integration test |
| `apps/web`, `apps/mobile` | no test framework/files; dependencies not installed | `bun install` + adding a test runner (not part of this task) |

---

## 6. Integration checks (combined changes)

| Check | Result |
|---|---|
| Full solution Release build, warnings-as-errors | pass |
| All six test projects, Release | 477/477 executed tests pass, 0 fail |
| Module dependency direction (Platform → Shared only) | preserved: `Platform.Api`/`Platform.Infrastructure` reference `Construction.Web`; no reference from Shared to Platform |
| Registration wiring resolves (`Program.cs` + `PlatformInfrastructureRegistration`) | exercised by `Construction.Api.Tests` (host boots, modules discovered) and Web in-process host tests |
| DI sanity for new services (fail-closed defaults in Production, fakes in Development) | compiles and host tests boot with Development profile; the Production swap path in `Program.cs` is code-reviewed only, **not integration-tested** |
| Migrations all additive, no drops of existing objects | verified by inspection of the six migration files |
| Error-code catalogue invariant (prefix + ar/en for every code) | enforced by `PlatformErrorCodesTests` (green) |
| No secret material added | `grep` of new files: no connection strings/tokens except pre-existing test constants in `LivePostgreSqlTenantIsolationTests.cs` (localhost dev credentials, unchanged) |

---

## 7. Per-ticket file split (exact)

Shared/cross-cutting files are assigned to **PLT-204**, the first branch, and arrive in later branches
through stacking (§8). Files marked *(hunk)* contain edits for more than one ticket and need
`git add -p` (or per-ticket commits) so each branch carries only its own lines.

### PLT-204 — `feat/plt-204-retries-dlq-replay`

```
backend/src/Shared/Construction.Kernel/Error.cs                                   (new BadRequest/PreconditionFailed/PreconditionRequired)
backend/src/Shared/Construction.Web/Errors/ErrorExtensions.cs                     (400/412/428 mapping)
backend/src/Shared/Construction.Persistence/TenantTransactionInterceptor.cs       (CC-01 transaction-local GUC)
backend/src/Modules/Platform/Platform.Contracts/PlatformErrorCodes.cs             (platform. prefix + ar/en catalogue)
backend/src/Modules/Platform/Platform.Core/Application/Audit/IAuditLog.cs
backend/src/Modules/Platform/Platform.Infrastructure/Audit/PostgresAuditLog.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121093000_AddAuditLog.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/Outbox/DeadLetterSingleConsumerGuardTests.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/PlatformErrorCodesTests.cs
backend/tests/Shared/Construction.Web.Tests/Errors/ErrorExtensionsTests.cs         (hunk)
```

Not in this branch: the pre-existing dispatcher/retry implementation (`Jobs/Outbox/*`,
`DeadLetterService`, `AddOutboxDead*` migrations) — it was already on `main` and is unchanged.

### PLT-205 — `feat/plt-205-platform-services`

```
backend/src/Shared/Construction.Web/Idempotency/IIdempotencyStore.cs
backend/src/Shared/Construction.Web/Idempotency/RequestHasher.cs
backend/src/Shared/Construction.Web/Idempotency/IdempotencyModels.cs
backend/src/Shared/Construction.Web/Idempotency/IdempotencyOptions.cs
backend/src/Shared/Construction.Web/Idempotency/IdempotencyMetadata.cs
backend/src/Shared/Construction.Web/Idempotency/IdempotencyDecisionEngine.cs
backend/src/Shared/Construction.Web/Idempotency/InMemoryIdempotencyStore.cs
backend/src/Shared/Construction.Web/Idempotency/IdempotencyMiddleware.cs
backend/src/Shared/Construction.Web/WebServiceCollectionExtensions.cs             (hunk: IdempotencyOptions registration)
backend/src/Modules/Platform/Platform.Infrastructure/Idempotency/PostgresIdempotencyStore.cs
backend/src/Modules/Platform/Platform.Infrastructure/Jobs/Runner/IdempotencyPurgeJob.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121090000_AddIdempotencyKeys.cs
backend/src/Host/Construction.Api/Program.cs                                     (hunk: UseMiddleware + Development store swap)
backend/tests/Shared/Construction.Web.Tests/Idempotency/RequestHasherTests.cs
backend/tests/Shared/Construction.Web.Tests/Idempotency/IdempotencyDecisionEngineTests.cs
backend/tests/Shared/Construction.Web.Tests/Idempotency/InMemoryIdempotencyStoreTests.cs
```

### PLT-206 — `feat/plt-206-number-series` (Platform Number Series)

```
backend/src/Modules/Platform/Platform.Core/Domain/NumberSeries/NumberSeriesRules.cs
backend/src/Modules/Platform/Platform.Core/Application/NumberSeries/INumberAllocator.cs
backend/src/Modules/Platform/Platform.Core/Application/NumberSeries/INumberSeriesStore.cs
backend/src/Modules/Platform/Platform.Contracts/NumberSeriesContracts.cs
backend/src/Modules/Platform/Platform.Api/NumberSeriesEndpoints.cs
backend/src/Modules/Platform/Platform.Infrastructure/NumberSeries/PostgresNumberAllocator.cs
backend/src/Modules/Platform/Platform.Infrastructure/NumberSeries/PostgresNumberSeriesStore.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121100000_AddNumberSeries.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/NumberSeries/NumberSeriesRulesTests.cs
```
*(hunk)* `Platform.Api/PlatformApiRegistration.cs` (`AddNumberSeriesPolicies`, `MapNumberSeriesEndpoints`),
`Platform.Infrastructure/PlatformInfrastructureRegistration.cs`, `Platform.Module/PlatformModule.cs`.

Scope: **Platform Number Series** (Platform Foundation / PLT-E8, spec 01 §4.9). Cost Control and Cash
Flow are separate modules and are not part of PLT-206; the "Cost/Cash" wording in the ticket list does not
match the reviewed project specification, so this slice claims no monetary behaviour. The `Cash` module
remains an empty scaffold.

### PLT-207 — `feat/plt-207-file-services`

```
backend/src/Modules/Platform/Platform.Core/Domain/Files/FilePolicy.cs
backend/src/Modules/Platform/Platform.Core/Application/Files/FilePorts.cs
backend/src/Modules/Platform/Platform.Core/Application/Files/IFileService.cs
backend/src/Modules/Platform/Platform.Contracts/FileContracts.cs
backend/src/Modules/Platform/Platform.Api/FileEndpoints.cs
backend/src/Modules/Platform/Platform.Infrastructure/Files/PostgresFileService.cs
backend/src/Modules/Platform/Platform.Infrastructure/Files/InMemoryObjectStorage.cs
backend/src/Modules/Platform/Platform.Infrastructure/Files/UnavailableAdapters.cs
backend/src/Modules/Platform/Platform.Infrastructure/Files/FileAccessCheckers.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121110000_AddFiles.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/Files/FilePolicyTests.cs
```
*(hunk)* registration + `Program.cs` Development adapters, `PlatformModule.cs` permissions.

### PLT-208 — `feat/plt-208-approval-services`

```
backend/src/Modules/Platform/Platform.Core/Domain/Approvals/ApprovalPolicyMatcher.cs
backend/src/Modules/Platform/Platform.Core/Application/Approvals/ApprovalPorts.cs
backend/src/Modules/Platform/Platform.Contracts/ApprovalContracts.cs
backend/src/Modules/Platform/Platform.Api/ApprovalEndpoints.cs
backend/src/Modules/Platform/Platform.Infrastructure/Approvals/PostgresApprovalService.cs
backend/src/Modules/Platform/Platform.Infrastructure/Approvals/FakeApproverDirectory.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121120000_AddApprovals.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/Approvals/ApprovalPolicyMatcherTests.cs
```
*(hunk)* registration + `Program.cs` Development directory, `PlatformModule.cs` permissions.

### PLT-209 — `feat/plt-209-notifications`

```
backend/src/Modules/Platform/Platform.Core/Domain/Notifications/NotificationRules.cs
backend/src/Modules/Platform/Platform.Core/Application/Notifications/NotificationPorts.cs
backend/src/Modules/Platform/Platform.Contracts/NotificationContracts.cs
backend/src/Modules/Platform/Platform.Api/NotificationEndpoints.cs
backend/src/Modules/Platform/Platform.Infrastructure/Notifications/PostgresNotificationService.cs
backend/src/Modules/Platform/Platform.Infrastructure/Notifications/EmailAdapters.cs
backend/src/Modules/Platform/Platform.Infrastructure/Jobs/Runner/NotificationDispatchJob.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121130000_AddNotifications.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/Notifications/NotificationRulesTests.cs
```
*(hunk)* registration + `Program.cs` Development resolver, `PlatformModule.cs` permissions.

### Documentation files

```
docs/team/PLT-204-209-EXECUTION-REPORT.md   (PLT-204 branch)
docs/team/PLT-204-209-QA-REPORT.md          (this file — PLT-209 branch, last)
docs/team/PLT-204-209-BRANCH-PLAN.md        (PLT-204 branch)
docs/team/D2-PROGRESS.md                    (hunk per ticket: add your ticket's row)
```

---

## 8. Transferring to a real clone and creating six PRs

This working copy has **no `.git`** (`git status` → `fatal: not a git repository`), no remote and no
`gh` CLI, so no branch or PR was created here — and none was faked.

### Merge order (dependency map)

```
main
 └── PLT-204  retries/DLQ/replay      (foundation: error types, catalogue, audit, tenant GUC)
      └── PLT-205  idempotency        (needs 204 error types + Web pipeline)
           ├── PLT-206  number series   (needs 205 idempotency metadata on POST/PATCH)
           ├── PLT-207  files           (needs 205)
           ├── PLT-208  approvals       (needs 205 + 204 catalogue)
           └── PLT-209  notifications   (needs 205 + 204 catalogue)
```
PLT-206…209 are independent of each other; stacking them all on 205 keeps one honest chain and avoids
conflicts in the three shared registration files. Order to merge: **204 → 205 → 206 → 207 → 208 → 209**.

### Step-by-step on a real clone

```bash
# 0. clone + verify the baseline actually builds/tests before touching anything
git clone <repo-url> bonyan && cd bonyan
git switch main && git pull --ff-only
dotnet test backend/Construction.sln -c Release

# 1. copy this working copy's files over the clone (paths are identical)
#    e.g. robocopy "C:\...\bonyan-main (1)\bonyan-main" "C:\...\bonyan" /E /XD .git bin obj node_modules

# 2. PLT-204
git switch -c feat/plt-204-retries-dlq-replay main
git add -A -- backend/src/Shared/Construction.Kernel/Error.cs \
  backend/src/Shared/Construction.Web/Errors/ErrorExtensions.cs \
  backend/src/Shared/Construction.Persistence/TenantTransactionInterceptor.cs \
  backend/src/Modules/Platform/Platform.Contracts/PlatformErrorCodes.cs \
  backend/src/Modules/Platform/Platform.Core/Application/Audit \
  backend/src/Modules/Platform/Platform.Infrastructure/Audit \
  backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121093000_AddAuditLog.cs \
  backend/tests/Modules/Platform/Platform.Domain.Tests/Outbox/DeadLetterSingleConsumerGuardTests.cs \
  backend/tests/Modules/Platform/Platform.Domain.Tests/PlatformErrorCodesTests.cs \
  docs/team/PLT-204-209-EXECUTION-REPORT.md docs/team/PLT-204-209-BRANCH-PLAN.md
git add -p backend/tests/Shared/Construction.Web.Tests/Errors/ErrorExtensionsTests.cs docs/team/D2-PROGRESS.md
dotnet test backend/tests/Modules/Platform/Platform.Domain.Tests -c Release --filter "FullyQualifiedName~DeadLetterSingleConsumerGuard|FullyQualifiedName~PlatformErrorCodes"
git commit -m "feat(platform): PLT-204 harden retries, dead letters and replay"
git push -u origin feat/plt-204-retries-dlq-replay

# 3. PLT-205 (branched from 204 while 204 is unmerged)
git switch -c feat/plt-205-platform-services feat/plt-204-retries-dlq-replay
# ... add only the PLT-205 files from §7 ...
dotnet test backend/tests/Shared/Construction.Web.Tests -c Release --filter "FullyQualifiedName~Idempotency"
git commit -m "feat(platform): PLT-205 Idempotency-Key middleware and store"
git push -u origin feat/plt-205-platform-services

# 4. PLT-206 … PLT-209: same pattern, each branched from feat/plt-205-platform-services
#    (or from the previously created ticket branch if you prefer a single linear stack)
```

PR creation (no `gh` here; run where it exists):

```bash
gh pr create --base feat/plt-204-retries-dlq-replay --head feat/plt-205-platform-services \
  --title "PLT-205: Idempotency-Key middleware" --body "Depends on PLT-204 (stacked). Retarget to main after PLT-204 merges."

# independent tickets (206–209) target the 205 branch first:
gh pr create --base feat/plt-205-platform-services --head feat/plt-206-number-series  --title "PLT-206: Implement Platform Number Series"
gh pr create --base feat/plt-205-platform-services --head feat/plt-207-file-services  --title "PLT-207: file services"
gh pr create --base feat/plt-205-platform-services --head feat/plt-208-approval-services --title "PLT-208: approval services"
gh pr create --base feat/plt-205-platform-services   --head feat/plt-209-notifications --title "PLT-209: notifications"
```

After PLT-204 merges: `gh pr edit <pr> --base main` for PLT-205, merge it, then retarget 206–209 to
`main` in the same way. Never merge automatically; never push to `main`.

### Guardrails

- Do not commit `bin/`, `obj/`, `.vs/`, `node_modules/`.
- The six raw-SQL migrations have no `.Designer.cs` and the EF snapshot is unchanged — committing them
  as-is is intentional; regenerate properly in a separate migration-hygiene ticket.
- Keep `docs/team/PLT-204-209-QA-REPORT.md` (this file) with the PLT-209 PR, since it reports the
  combined run after all six are applied.

---

## 9. Bottom line

- 525 tests discovered across 6 projects; **477 executed and passed, 48 skipped by environment
  opt-out, 0 failed**; Release build clean with warnings-as-errors.
- Two genuine defects were found and fixed by this regression (idempotency hash canonicalisation and
  the live-PG fixture teardown); the rest were compile/test-authoring errors.
- Nothing that needs PostgreSQL, Docker, S3 or an email provider is claimed as verified.
- Branches and PRs remain impossible in this copy: no `.git`, no remote, no `gh`.
