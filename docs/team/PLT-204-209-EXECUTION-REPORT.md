# PLT-204 → PLT-209 — Execution Report

Date: 2026-11-21 · Scope: `PLT-204-209-executive-implementation-plan.md`
Branch: **not created — the working copy has no `.git`** (see §H).

---

## A. Ticket status

| Ticket | Status | Implementation summary | Tests passed / failed | Remaining blockers |
|---|---|---|---|---|
| **PLT-204** Retries, DLQ, replay | `IMPLEMENTED — TESTS NOT VERIFIED` (DB behaviour unverified) | Existing outbox dispatcher/retry/dead-letter path was audited and hardened (CC-05 audit log; per-tenant GUC fix; dead-letter replay/discard codes). **R-204-01 (per-consumer fan-out) deliberately not implemented** — the recipe's single-consumer model is still authoritative; a guard test pins it. R-204-02…08 implemented (prefix fix, problem+json, financial-event operator rule, no stack traces). | 133 platform unit tests pass (incl. dead-letter contract + new guard); **8 Postgres dead-letter tests skipped** (no `D2_TEST_PG`). Failed: 0 | No PostgreSQL instance → RLS/replay/atomicity/concurrency proofs cannot run. Open schema decision §G.4 |
| **PLT-205** Idempotency-Key middleware | `IMPLEMENTED — TESTS PASS` (unit) | Canonical body hashing, metadata modes (`Required`/`Optional`/`Disabled`), decision engine (replay / key-reuse 422 / in-progress 409 / takeover), `IIdempotencyStore` with in-memory + Postgres implementations, purge job, middleware placed after auth/rate-limit, 24 h retention, response replay incl. headers. | 20/20 idempotency tests green; `Construction.Web.Tests` 130/130; full build green | Postgres store concurrency (`ON CONFLICT` claim) not exercised without a database |
| **PLT-206** Cost / Cash (number series) | `IMPLEMENTED — TESTS PASS` (unit) | `NumberSeriesRules` (prefix/scope/format/reset/start validation, immutability after lock), `INumberAllocator`, Postgres advisory-lock allocator, append-only `number_allocations` with `attach_number_allocation` SECURITY DEFINER, endpoints, audit rows. Money untouched (no new monetary code path; existing `decimal`/`numeric(18,4)` conventions preserved). | `NumberSeriesRulesTests` green (part of the 133) | Allocation under real concurrency + RLS unverified |
| **PLT-207** Files / documents | `IMPLEMENTED — TESTS NOT VERIFIED` (provider-dependent paths unverified) | Upload sessions w/ single-use token, size/type allowlist + **magic-byte** sniffing, checksum + size echo verification, object storage port (`S3`-shaped presign), quarantine→clean→infected transition, legal hold blocking delete, download authorization port, link attachments, delete marks row + object. Fail-closed adapters (`UnavailableObjectStorage`, `UnavailableFileScanner`, `DenyAllFileAccessChecker`) in non-Development. | `FilePolicyTests` green; **never claims a scan succeeded** — unavailable scanner returns 503 | No S3/MinIO or malware scanner configured; presign/scan/quarantine flows unverified end-to-end |
| **PLT-208** Approvals | `IMPLEMENTED — TESTS PASS` (unit) | Policy matching (entity/amount/currency/scope precedence), immutable rule snapshot per instance, approver resolution through `IApproverDirectory` (fake only in Development), approve/reject with `If-Match`+`instance_version`, self-approval rejection, append-only decisions with DB trigger, expiry, simulate endpoint, outbox events. | `ApprovalPolicyMatcherTests` green | No real approver directory integration exists in the repo → `FakeApproverDirectory` is Development-only; Postgres concurrency unverified |
| **PLT-209** Notifications | `IMPLEMENTED — TESTS PASS` (unit) | Intents, ar/en templates seeded in migration, in-app inbox + read/preferences, mandatory-notification bypass of preferences/quiet hours, quiet hours in tenant timezone, dedupe by `dedupe_key`, at-least-once email with retry/dead state and delivery status rows, dispatch job. | `NotificationRulesTests` green | **Email is at-least-once, not exactly-once**; no SMTP/provider configured (`UnavailableEmailSender` → `Skipped`), digest batching not implemented (not in recipe scope) |

---

## B. Changed files

### Created — shared
- `backend/src/Shared/Construction.Web/Idempotency/RequestHasher.cs` — canonical JSON hashing, key fingerprint, constant-time compare.
- `…/Idempotency/IIdempotencyStore.cs`, `IdempotencyModels.cs`, `IdempotencyOptions.cs`, `IdempotencyMetadata.cs`, `IdempotencyDecisionEngine.cs`, `InMemoryIdempotencyStore.cs`, `IdempotencyMiddleware.cs`.
- `backend/src/Shared/Construction.Persistence/Migrations/*` helpers used by the new migrations (RLS/append-only helpers as added).

### Created — Platform
- `Platform.Contracts/PlatformErrorCodes.cs` was **rewritten**; new contracts: `Idempotency`, `NumberSeries`, `Files`, `Approvals`, `Notifications` (DTOs, ports, permission constants).
- `Platform.Infrastructure/Idempotency/PostgresIdempotencyStore.cs`, `NumberSeries/PostgresNumberAllocator.cs`, `NumberSeries/PostgresNumberSeriesStore.cs`, `Files/PostgresFileService.cs`, `Files/InMemoryObjectStorage.cs`, `Files/UnavailableAdapters.cs`, `Files/FileAccessCheckers.cs`, `Approvals/PostgresApprovalService.cs`, `Approvals/FakeApproverDirectory.cs`, `Notifications/PostgresNotificationService.cs`, `Notifications/EmailAdapters.cs`, `Audit/PostgresAuditLog.cs`, `Jobs/*` purge/dispatch jobs.
- `Platform.Api/NumberSeriesEndpoints.cs`, `FileEndpoints.cs`, `ApprovalEndpoints.cs`, `NotificationEndpoints.cs`, `PlatformApiResults.cs`; `PlatformApiRegistration.cs` extended.
- Migrations: `20261121090000_AddIdempotencyKeys.cs`, `20261121093000_AddAuditLog.cs`, `20261121100000_AddNumberSeries.cs`, `20261121110000_AddFiles.cs`, `20261121120000_AddApprovals.cs`, `20261121130000_AddNotifications.cs`.

### Created — tests
- `Platform.Domain.Tests/NumberSeries/NumberSeriesRulesTests.cs`, `Files/FilePolicyTests.cs`, `Approvals/ApprovalPolicyMatcherTests.cs`, `Notifications/NotificationRulesTests.cs`, `PlatformErrorCodesTests.cs`, `Outbox/DeadLetterSingleConsumerGuardTests.cs`.
- `Construction.Web.Tests/Idempotency/RequestHasherTests.cs`, `IdempotencyDecisionEngineTests.cs`, `InMemoryIdempotencyStoreTests.cs`.

### Modified
- `Construction.Kernel/Error.cs` — `BadRequest`, `PreconditionFailed`, `PreconditionRequired`; `ErrorType` extended.
- `Construction.Web/Errors/ErrorExtensions.cs` — 400/412/428 mapping.
- `Construction.Persistence/TenantTransactionInterceptor.cs` — tenant GUC now transaction-local (`set_config(..., true)`), fixing cross-tenant/connection-pool leakage.
- `Host/Construction.Api/Program.cs` — idempotency middleware after auth/rate limiter; Development-only fake adapters.
- `Platform.Module/PlatformModule.cs` — new permissions.
- `Platform.Infrastructure/PlatformInfrastructureRegistration.cs` — services/jobs.
- `Platform.Infrastructure.csproj`, `Platform.Api.csproj` — project references (`Construction.Web`, `Platform.Contracts`); removed one downgraded `DependencyInjection.Abstractions` reference.
- `Shared/Construction.Web.Tests/Errors/ErrorExtensionsTests.cs` — extended for the new mappings.

No files were deleted.

---

## C. Database changes

All additive, schema `platform`, `down` implemented.

| Migration | Objects |
|---|---|
| `AddIdempotencyKeys` | table `platform.idempotency_keys` (PK `(tenant_id, key_fingerprint)`), `request_hash`, response snapshot (`status`, `content_type`, `response_body`, `response_headers`, `replay_too_large`), `expires_at` + index; tenant RLS forced |
| `AddAuditLog` | table `platform.audit_log` (append-only), tenant/actor/action/entity indexes; RLS forced; UPDATE/DELETE revoked + trigger |
| `AddNumberSeries` | `platform.number_series`, `platform.number_allocations` (append-only), `attach_number_allocation(...)` SECURITY DEFINER function, unique `(tenant_id, code, scope_key)` |
| `AddFiles` | `platform.files`, `platform.file_links`; checksum/size/scan-state/legal-hold columns; tenant RLS forced |
| `AddApprovals` | `platform.approval_policies`, `platform.approval_instances` (immutable `rules_snapshot` jsonb, `version`), `platform.approval_decisions` (append-only, trigger); RLS forced |
| `AddNotifications` | `platform.notifications`, `platform.notification_preferences`, `platform.email_deliveries`, `platform.notification_templates` (+ ar/en seed); RLS forced |

Manual action required: none beyond `dotnet ef database update` / app migration run against a real instance. **The EF model snapshot was not updated** (migrations are raw-SQL); the next `dotnet ef migrations add` would re-scaffold these tables — resolve before adding migrations.

---

## D. API and contract changes

New endpoints (all under the existing versioned, tenant-aware pipeline):

| Method | Route | Purpose |
|---|---|---|
| GET/PATCH | `/api/v1/number-series`, `/{code}` | list / read / update rules (immutable fields rejected after lock) |
| GET | `/api/v1/number-series/{code}/allocations` | allocation history |
| POST | `/api/v1/files/uploads` | open upload session (size/type/magic checks, presigned PUT) |
| POST | `/api/v1/files/{id}/complete` | verify size/checksum, enqueue scan |
| GET | `/api/v1/files/{id}` | metadata |
| GET | `/api/v1/files/{id}/download` | short-lived presigned GET, authorization-checked |
| DELETE | `/api/v1/files/{id}` | soft delete (blocked by legal hold) |
| POST/DELETE | `/api/v1/files/{id}/links` | attach/detach file to an entity |
| POST | `/api/v1/approvals/requests` | create request from matched policy + snapshot |
| GET | `/api/v1/approvals/requests/{id}` | instance + decisions |
| POST | `/api/v1/approvals/requests/{id}/approve` \| `/reject` | decision (`If-Match`/version required) |
| GET/PUT | `/api/v1/approvals/policies` | list / upsert policies |
| POST | `/api/v1/approvals/simulate` | dry-run matching |
| GET | `/api/v1/notifications` | inbox |
| POST | `/api/v1/notifications/{id}/read` | mark read |
| GET/PUT | `/api/v1/notifications/preferences` | per-category channel prefs |
| GET/PUT | `/api/v1/notifications/templates/{key}` | ar/en template admin |

Contracts/interfaces added: `IIdempotencyStore`, `INumberAllocator`, `INumberSeriesStore`, `IObjectStorage`, `IFileScanner`, `IFileAccessChecker`, `IApprovalStore`/`IApproverDirectory`, `INotificationStore`, `IEmailSender`, `INotificationRecipientResolver`, `IAuditLog`. New integration/domain events: `approval.requested`, `approval.decided`, `notification.dispatch` (all written to the existing outbox — **no second messaging system**). Permissions added: `platform.files.*`, `platform.approvals.*`, `platform.notifications.*`, `platform.number_series.*`.

`Idempotency-Key` behaviour: required on `POST/PUT/PATCH` for endpoints marked required (approvals, file mutations, number-series PATCH); optional otherwise on `/api/v1` POST; conflict responses are RFC 9457 `application/problem+json` with `platform.idempotency_key_reused` (422) / `platform.idempotency_request_in_progress` (409).

---

## E. Test evidence

| Command | Result |
|---|---|
| `dotnet build backend/Construction.sln -warnaserror -m:1 -nodeReuse:false --no-restore` | **Build succeeded** (all projects, warnings as errors) |
| `dotnet test backend/tests/Modules/Platform/Platform.Domain.Tests --filter "Category!=Slow"` | **Passed! Failed: 0, Passed: 211, Skipped: 8, Total: 219** (8 skips = `DeadLetterPostgresTests`, need `D2_TEST_PG`) |
| `dotnet test backend/tests/Modules/Platform/Platform.Domain.Tests --filter "<PLT-204-209 test classes>"` | **Failed: 0, Passed: 133** |
| `dotnet test backend/tests/Shared/Construction.Web.Tests` | **Passed! Failed: 0, Passed: 130** (20 are the new idempotency tests) |
| `dotnet test backend/tests/Shared/Construction.Kernel.Tests` | **Passed! Failed: 0, Passed: 63** |
| Postgres-backed suites (`DeadLetterPostgresTests`, `Tenant.Isolation.Tests`, `Host`, `E2E`) | **Not run** — no PostgreSQL, no Docker, `D2_TEST_PG` unset |
| Architecture tests | **Not runnable** — `backend/tests/Architecture.Tests` exists but contains no project/sources |
| Formatting | No repo formatter configured (`dotnet format` not wired into CI here); analyzers run via `-warnaserror` |

New tests cover: canonical hashing equality/difference and constant-time compare; every replay decision branch; single-winner claim, stale takeover, release-after-completion, expiry, purge, principal/tenant key scoping; number-series field validation, immutability after lock, formatting/padding; file size/type/magic-byte rules and quarantine gating; approval matching precedence and self-approval; notification rendering/escaping/quiet-hours/window logic; and the error-catalogue invariant (every code starts `platform.` and has ar+en text).

---

## F. Security review

- **Tenant isolation** — every new table has `tenant_id NOT NULL` + forced RLS policy on `app.tenant_id`; all queries filter by tenant; `TenantTransactionInterceptor` now sets the GUC **transaction-locally**, closing the pooled-connection leak. `attach_number_allocation` is the only `SECURITY DEFINER` function (no dynamic SQL, fixed search_path) so the caller can write allocations without broader grants. ⚠️ RLS behaviour itself could not be executed (no DB).
- **Authorization** — each endpoint is permission-gated via the module permission list; download and file links additionally go through `IFileAccessChecker` (deny-all by default outside Development).
- **Data ownership** — files record `owner_user_id`; delete/download/link require ownership or an explicit grant; links are tenant-scoped.
- **Idempotency** — keys are scoped by `(tenant, principal, key, method, route)`; fingerprint stored instead of the raw key; body hash covers method+route+content-type+canonical body, so a changed body cannot replay another response.
- **Concurrency** — approvals require the current `version` (`If-Match`); decisions are append-only (UPDATE/DELETE revoked + trigger); allocation uses a Postgres advisory lock; idempotency claims rely on a primary-key `ON CONFLICT ... DO NOTHING` insert.
- **Secrets** — no connection strings, keys or tokens added; provider adapters read configuration only and fail closed when absent.
- **File security** — allowlist extension/MIME + magic-byte sniffing, size limits, checksums verified server-side, quarantine before any download is possible, legal hold blocks deletion, downloads are short-lived presigned URLs behind authorization. Scanning is never faked: with no scanner configured, `complete` reports unavailable rather than "clean".
- **Approval integrity** — matching rules are snapshotted immutably on the instance; self-approval is rejected; a decision cannot be revised (append-only), and repeated decisions return a conflict.
- **Notification privacy** — templates render only registered placeholders (no expression evaluation); unknown placeholders stay inert; user preferences scoped per user+tenant.

---

## G. Remaining blockers

**Environment (not code):**
1. No PostgreSQL / Docker / `D2_TEST_PG` → all DB, RLS, append-only, concurrency and replay-atomicity proofs for PLT-204…209 are unverified. Reproduce with: `D2_TEST_PG="Host=localhost;Database=construction_test;Username=…" dotnet test backend/tests/Modules/Platform/Platform.Domain.Tests`.
2. `backend/tests/Architecture.Tests` contains no project, so "existing architecture tests" cannot be run.
3. No S3/MinIO and no malware scanner → presign, quarantine and scan transitions are unverified beyond unit rules.

**Business decisions (not invented):**
4. R-204-01 per-consumer fan-out vs the recipe's single-consumer retry state — implemented the recipe; `DeadLetterSingleConsumerGuardTests` fails the moment fan-out lands without the schema/API gaining a `consumer` dimension.
5. Dead-letter cutoff: 5th failure (recipe) vs after 5 retries (BR-PLT-63) — recipe kept.
6. Approver directory: the repo has no HR/identity integration, so `IApproverDirectory` is a port with a Development-only fake; production wiring is a follow-up.
7. Email semantics: **at-least-once**; exactly-once is not claimed. Digest batching is not implemented (not specified in the recipe).
8. EF model snapshot not updated for the raw-SQL migrations.

---

## H. Git status

- Current branch: **none** — `git status` reports `fatal: not a git repository`; the workspace has no `.git` directory.
- Requested branch `feat/platform-plt-204-209-execution` **could not be created**.
- Working tree: all work above exists as uncommitted files on disk.
- Committed: **no**. Pushed: **no**. Deployed: **no**. Merged: **no**.

To finish the git portion on a real clone: `git switch -c feat/platform-plt-204-209-execution`, then commit the files listed in §B.
