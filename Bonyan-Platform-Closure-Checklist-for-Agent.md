# Bonyan / Construction Connect — Platform Closure & Implementation Checklist

**Prepared:** 2026-11-14  
**Repository:** `bonyancore/bonyan` (expected local path: `~/Desktop/proDev/bonyan`)  
**Planning source:** Notion — [Backend ticket list](https://app.notion.com/p/4016c0755cae803aaf3cee200a395569)  
**Audit baseline supplied:** Comprehensive Planning vs. Implementation Audit dated 2026-11-14, commit `5571c68`  
**Target tickets:** `PLT-101`–`PLT-104` and `PLT-201`–`PLT-204`

> This is an implementation and closure work order for the coding agent. It is not permission to change Notion, push commits, open/merge PRs, provision cloud infrastructure, or perform destructive database operations. Work only in the implementation branch explicitly selected by the user.

## 1. Mission

Bring the existing implementations of the eight target tickets to their original Notion acceptance criteria, with tests and repository conventions satisfied, so each ticket can be closed based on verifiable evidence.

This is a **closure-focused implementation pass**, not a general production-readiness rewrite and not an invitation to implement the entire Construction Connect roadmap. The objective is to prove behavior, especially:

- tenant isolation under real PostgreSQL Row-Level Security (RLS);
- transactional outbox delivery to a real test consumer;
- idempotent event processing;
- reliable Worker/job execution, timeouts and persistence;
- retry, dead-letter, replay and discard behavior;
- Kernel, module registration and persistence conventions.

Use the **current checked-out repository** and **current Notion specifications** as sources of truth. The supplied audit is a dated snapshot; revalidate every finding against the actual branch before changing code.

## 2. Sources of truth and scope

Use this order:

1. Relevant detailed module specifications and approved architecture/engineering rules in Notion and the repository.
2. The Notion Backend ticket list for ticket scope and explicit Done conditions.
3. Current checked-out code, migrations, tests, configuration and git history for actual implementation state.
4. The supplied 2026-11-14 audit as findings to verify, not unquestionable truth.

Fetch/read relevant specs and inspect at least `README.md`, `docs/modules/README.md`, `docs/sprint-0-checklist.md`, relevant ADRs/RFCs, `docs/team/D2-PROGRESS.md`, and any ticket acceptance-test specifications present in the repo. Record source names/URLs and conflicts. Do not silently replace original acceptance criteria with recommendations from the audit.

The Notion Backend ticket list defines:

- `PLT-101`–`PLT-104`: initial Platform foundation.
- `PLT-201`–`PLT-204`: Messaging, Outbox, Worker/Job Runner and Retries/Dead Letter.

Do not silently pull `PLT-105`–`PLT-113` or `PLT-205`–`PLT-211` into these tickets. If a later ticket is a genuine prerequisite, document it separately and implement only the minimum approved prerequisite needed for the current acceptance criteria. Do not turn this pass into full Authentication, Audit Log, CI/CD, Files, Approvals, Notifications or other roadmap implementation unless an in-scope spec explicitly requires it.

Keep the Notion Release 1 exclusions out of scope: voice/AI, Sourcing, Subcontracts, Estimating, Closeout, Equipment, Stores and portals.

## 3. Mandatory operating rules

### Before changes

- [ ] Confirm repository root, current branch, HEAD SHA and `git status --short --branch`.
- [ ] Do not overwrite, reset, stash, amend or discard user work.
- [ ] If the working tree is not clean or the branch is not the intended implementation branch, stop and report; do not switch/create branches unless explicitly instructed.
- [ ] Inspect all eight tickets' actual code, migrations, tests and configuration before planning modifications.
- [ ] Produce a baseline matrix: requirement → current implementation → evidence → gap → proposed change.

### While implementing

- Work incrementally in relevant module/shared-library ownership boundaries.
- Do not duplicate abstractions or rewrite working components without a demonstrated requirement.
- Follow current framework, package, nullable, analyzer, formatting and naming conventions. No incidental framework/package upgrades.
- Preserve public contracts unless a ticket requires a change; update all in-repo consumers and tests together when a contract changes.
- Use DI and abstractions. Hosted services must not directly construct infrastructure dependencies where DI can supply them.
- Propagate `CancellationToken`; use async APIs correctly; avoid sync-over-async.
- Never swallow failures, mark messages successful before actual delivery, or treat unsupported behavior as success.
- Fakes are appropriate for unit tests/contracts, not as substitutes for required runtime behavior.
- New tenant-owned tables require migrations and appropriate RLS policies.
- EF Core InMemory tests are not proof of PostgreSQL RLS, raw SQL, locking or transaction semantics.
- Follow approved ADRs/RFCs. Do not invent architectural decisions or privilege grants.

### Notion cross-ticket rules

The Backend ticket list says every ticket must:

- work only in the relevant module folders and migrations;
- expose cross-module needs through `.Contracts` with an in-memory fake;
- register modules in `AppModules.cs`;
- finish with tests;
- add RLS policies to new tenant-owned tables;
- add `module_code` to every endpoint;
- add error codes to the message catalogue in Arabic and English.

Apply equivalent repository conventions to shared technical libraries where module-specific rules do not literally apply. Do not add endpoints/tables unnecessarily just to tick a box.

### Completion rules

Use `PASS`, `PARTIAL`, `FAIL`, `BLOCKED` or `N/A` for every criterion. A ticket is **Ready to Close** only when all explicit Done conditions pass, required tests actually ran, and no ticket-level blocker remains. “Merged”, “build succeeds” or “unit tests pass with fakes” is not enough. Do not update Notion statuses, push, merge, deploy or modify remote settings.

## 4. Ticket-by-ticket closure checklist

### PLT-101 — Kernel project

**Notion scope:** Money, Quantity, Result, IClock, UUID v7 IDs, Errors and TenantContext.  
**Done when:** Other modules can reference it and it has unit tests.

- [ ] Verify `Construction.Kernel` contains `Money`, `Quantity`, `Result`, `IClock`, UUID v7 ID generation, Errors and TenantContext.
- [ ] Verify domain primitives enforce invariants and are immutable/defensive where appropriate.
- [ ] Verify IDs are actually UUID v7, not random UUIDs or custom timestamp-like identifiers.
- [ ] Verify Kernel has no EF Core, ASP.NET Core, Npgsql or infrastructure dependencies.
- [ ] Verify other modules can reference Kernel without circular dependencies or infrastructure leakage.
- [ ] Verify error/result behavior is consistent and consumable by other modules.
- [ ] Add/complete unit tests for valid/invalid values, boundaries and relevant equality/serialization behavior.
- [ ] Build dependent projects to prove public API usability.

**Audit claims to re-check:** Kernel primitives reportedly exist and 63 unit tests pass. Verify actual test projects, coverage and UUID implementation.

**Evidence:** source paths/types, dependency/build evidence, exact test command and results.

### PLT-102 — Module template and registration

**Notion scope:** `IModule` and module registration in `AppModules.cs`, supporting services, endpoints and migrations.  
**Done when:** A new module can be created from the template and appears in the Host with one line.

- [ ] Verify `IModule` supports services, endpoints and migrations.
- [ ] Verify `AppModules.cs` is the authoritative module list/composition point.
- [ ] Verify Host composition does not depend on each module's internal Infrastructure implementation.
- [ ] Verify the current project layout and ADR-backed composition-root pattern are documented and consistent.
- [ ] Prove one-line registration with an automated test or minimal module-template/conformance test.
- [ ] Verify registration does not create duplicate services, conflicting endpoint/rate-limit policies or missing migrations.
- [ ] Verify existing modules are registered exactly once.

**Audit claims to re-check:** `IModule`, `PlatformModule` and the five-project pattern under ADR-001 reportedly exist. The pattern is acceptable only if consistent with the approved ADR and one-line Host registration still holds.

**Evidence:** interface/composition paths, ADR, registration test and Host build/startup evidence.

### PLT-103 — Persistence base

**Notion scope:** `BaseDbContext` with interceptors for created/updated by and at, `tenant_id` and `row_version`. **Depends on PLT-101.**

- [ ] Verify shared `BaseDbContext` exists and is used by intended module DbContexts.
- [ ] Verify created timestamp/actor are populated on insert.
- [ ] Verify updated timestamp/actor are updated on modification without overwriting creation metadata.
- [ ] Verify tenant ID is consistently assigned/handled from tenant context and cannot be supplied to bypass isolation.
- [ ] Verify `row_version` is configured and updated/checked using intended optimistic concurrency semantics.
- [ ] Verify interceptor ordering and behavior for Added/Modified entities and both SaveChanges paths.
- [ ] Add tests for created/updated metadata, missing actor, tenant propagation, row-version changes and concurrency conflicts.
- [ ] Verify shared persistence abstractions do not invert dependencies into Kernel/domain code.

**Audit claims to re-check:** `AuditSaveChangesInterceptor` and audit columns reportedly exist, but the audit does not prove complete `BaseDbContext`, `tenant_id` and `row_version` behavior.

**Evidence:** base context/configuration/interceptor paths and tests for every required behavior.

### PLT-104 — Tenancy and Row-Level Security

**Notion scope:** tenants table, tenant from token claim, `TenantScope`, RLS policy template and fail-closed behavior. **Depends on PLT-103.**  
**Exact Done condition:** With no tenant set, a query returns zero rows; Tenant B cannot read Tenant A's data even with raw SQL.

- [ ] Verify tenants entity/table/migration and tenant identity representation.
- [ ] Verify authenticated principal's tenant claim is authoritative in the application path.
- [ ] Verify untrusted request headers cannot override the authenticated tenant claim.
- [ ] Verify `TenantScope`/ambient setter safely enters/restores scopes, including nested and async execution.
- [ ] Verify tenant context is applied to every relevant connection/transaction using the repository's mechanism.
- [ ] Verify RLS policies are created by migrations for all in-scope tenant-owned tables.
- [ ] Verify policies are enabled/forced as required by the approved security model.
- [ ] Verify normal application role cannot bypass RLS (ownership, superuser, `BYPASSRLS`, grants).
- [ ] Verify no-tenant reads return **zero rows**, not merely an exception, unless the approved spec explicitly defines otherwise. Reconcile any invalid-UUID/cast-error fail-closed behavior against the literal Done condition.
- [ ] Verify Tenant B cannot read Tenant A through EF Core queries.
- [ ] Verify Tenant B cannot read Tenant A through raw SQL using the normal application DB role.
- [ ] Verify cross-tenant writes/updates/deletes are denied as required.
- [ ] Run tests against real PostgreSQL with the relevant role/privilege model. Use Testcontainers if available, otherwise a disposable test database.
- [ ] Ensure RLS tests are not silently skipped; report exact executed/passed/skipped counts.
- [ ] Ensure tenant isolation tests can be extended as tables/endpoints are added.

**Audit claims to re-check:** 14 RLS integration tests reportedly skipped; `DevHeaderAuthenticationHandler` reportedly handles Development auth while JWT bearer validation is absent; setting `app.tenant_id = ''` reportedly causes a UUID cast error. Test the literal zero-row requirement. The proposed `cc_outbox_dispatcher` role in RFC 0001 is mainly relevant to PLT-202: review the approved RFC and implement only least-privilege access; never grant broad RLS bypass casually.

**Scope boundary:** PLT-104 requires tenant resolution from a token claim; it does not automatically authorize implementing all PLT-106 login/refresh-token functionality. Use a validated test principal/token fixture or approved identity boundary if full identity-provider work belongs to PLT-106. A dev fake is not production authentication.

**Evidence:** migration/policy paths, role/grant configuration, exact PostgreSQL test command, raw SQL isolation results and proof no required test was skipped.

### PLT-201 — Messaging library

**Notion scope:** Event Envelope, `IOutbox`, handler interfaces and Idempotent Consumer base.

- [ ] Verify envelope includes fields required by approved contract: stable event ID, type, occurrence timestamp, tenant identity and optional aggregate/project/actor/correlation/causation metadata where specified.
- [ ] Verify event ID uses Kernel UUID v7 generation.
- [ ] Verify `IOutbox` exposes intended publish/stage contract without coupling Core projects to infrastructure.
- [ ] Verify typed handler interfaces are discoverable and usable by module consumers.
- [ ] Verify `IdempotentConsumer<T>` uses durable processed-event storage and a transaction boundary atomic with handler work.
- [ ] Verify duplicate `(consumer, event_id)` delivery is suppressed.
- [ ] Verify handler failure rolls back processed-event recording so retry can execute.
- [ ] Verify tenant context is established/restored correctly during async and nested event handling.
- [ ] Verify in-memory fakes exist for contracts other modules need.
- [ ] Check whether approved spec requires JSON Schema validation only as a library capability or wired into runtime publish/consume. Record the exact source; do not invent a requirement.
- [ ] If runtime validation is required, wire it at the correct boundary and test invalid events are rejected before persistence/handling.
- [ ] Ensure validator rejects unsupported schema constructs rather than silently accepting them.
- [ ] Ensure required envelope metadata is not discarded between envelope creation, persistence and delivery; coordinate PLT-202 changes where appropriate.

**Audit claims to re-check:** 18 Messaging tests reportedly pass; `IdempotentConsumer<T>` and processed-event stores exist; `JsonSchemaValidator` reportedly is not wired into runtime; outbox writer may persist payload only and drop correlation/causation/actor/project metadata.

**Evidence:** interfaces, consumer/store implementation, duplicate/failure/tenant-scope tests, schema contract decision and runtime test if required.

### PLT-202 — Transactional Outbox

**Notion scope:** Write outbox row in same transaction as business change; dispatch from Worker using PostgreSQL `FOR UPDATE SKIP LOCKED`.  
**Exact Done condition:** A test event reaches a test consumer exactly once.

- [ ] Verify outbox writer stages event in the same DbContext/unit of work and DB transaction as business mutation.
- [ ] Test atomic commit of business change + outbox row.
- [ ] Test rollback proves neither is committed if transaction fails.
- [ ] Verify dispatcher claims batches atomically using `FOR UPDATE SKIP LOCKED`.
- [ ] Verify concurrent workers cannot claim the same event simultaneously.
- [ ] Verify per-aggregate ordering matches approved specification.
- [ ] Verify lock leases are bounded and recover after worker termination.
- [ ] Replace any no-op delivery delegate with a real DI-resolved typed consumer registry/dispatch path.
- [ ] Ensure message is marked dispatched only after actual handler/broker publish succeeds.
- [ ] Ensure failures enter retry handling and are never marked successful.
- [ ] Verify end-to-end: business change + event commit → Worker claims → typed test consumer runs → observable effect recorded → duplicate delivery does not duplicate the effect.
- [ ] Clarify “exactly once”: dispatcher transport is normally at-least-once; prove one observable consumer effect through idempotent processing. Do not claim exactly-once network delivery without proof.
- [ ] Verify RLS permits Worker to claim intended cross-tenant events without allowing ordinary app users to bypass tenant isolation.
- [ ] If RFC 0001 requires a dedicated dispatcher role, implement its least-privilege policy/connection exactly as approved. Do not invent broad `BYPASSRLS` grants or weaken forced RLS.
- [ ] Test dispatcher role against PostgreSQL with multiple tenants; prove intended outbox access and no unrelated tenant-table access.
- [ ] Verify stored event retains all metadata required by approved envelope contract; add compatible migration if current schema drops it.
- [ ] Verify outbox tables/indexes have required migrations and RLS.
- [ ] Verify cross-module contracts follow `.Contracts` + in-memory fake conventions.

**Audit claims to re-check:** `OutboxDispatcherHostedService` reportedly passes `(_, _) => Task.CompletedTask`; forced RLS reportedly prevents cross-tenant claims using default app role; `EfOutboxWriter` reportedly persists payload only; current implementation reportedly simplifies earlier fan-out tables. Compare approved Notion spec, ticket and accepted ADR/RFC before deciding whether fan-out is required. Do not implement fan-out speculatively.

**Evidence:** PostgreSQL transaction tests, concurrency/ordering tests, real typed consumer end-to-end test, duplicate-effect proof, worker-role/RLS proof and metadata round-trip proof.

### PLT-203 — Worker host and Job Runner

**Notion scope:** Worker Host and Job Runner, queues, `job_runs` table and timeouts.

- [ ] Verify Worker is a separately runnable Host using repository hosting/DI conventions.
- [ ] Verify job catalog is extensible without modifying runner for every new job.
- [ ] Verify runner executes registered jobs and persists state to `platform.job_runs`.
- [ ] Verify run identity/idempotency prevents duplicate execution for same tenant/job/scheduled window per approved semantics.
- [ ] Verify advisory locks/concurrency control prevent simultaneous duplicate runs for same logical job/tenant.
- [ ] Verify timeout enforcement and cancellation propagation to job implementation.
- [ ] Verify failures persist useful status/error metadata and do not unexpectedly crash Worker.
- [ ] Verify retry/restart behavior against approved job semantics.
- [ ] Verify tenant-specific job enablement is persistent/configurable if required by detailed spec. Separate local in-memory fake from production implementation.
- [ ] Verify queue behavior to the extent explicitly required; do not assume scheduler alone satisfies a queue requirement.
- [ ] Verify cron/time-zone behavior against accepted format; document unsupported syntax and reject it clearly.
- [ ] Verify graceful shutdown/cancellation cannot leave runs falsely marked successful.
- [ ] Inspect `docs/team/tests/PLT-203-acceptance-spec.md` if present and authoritative; implement its missing conformance scenarios. Do not assume the audit's count of 25 is still accurate.
- [ ] Add PostgreSQL integration tests for run persistence, unique run keys and advisory locks.
- [ ] Verify hosted-service, scoped DbContext and repository DI lifetimes.

**Audit claims to re-check:** one `PlatformHeartbeatJob` reportedly registered; `DictionaryTenantJobEnablement` reportedly in-memory; 25 acceptance tests reportedly missing. Audit says in-process runner was selected under ADR-002; respect accepted ADR and do not replace it with Hangfire without an explicit decision.

**Evidence:** Worker startup/registration, runner tests, timeout/cancellation tests, PostgreSQL persistence/locking tests and reconciliation of every acceptance-spec scenario.

### PLT-204 — Retries, Dead Letter and Replay

**Notion scope:** Retries with backoff, dead-letter table and replay endpoint.

- [ ] Verify retry backoff follows approved schedule and is deterministic/tested.
- [ ] Verify attempt count increments exactly once per failed delivery attempt.
- [ ] Verify retry scheduling persists `next_attempt_at` and survives Worker restart.
- [ ] Verify terminal failure moves message to dead-letter storage without losing original event identity, tenant, type, payload and diagnostics.
- [ ] Verify dead-letter records have appropriate tenant RLS.
- [ ] Verify replay requeues original event safely and atomically.
- [ ] Verify concurrent replay cannot resolve/replay same open dead letter more than once.
- [ ] Verify discard requires a non-empty reason/note and cannot silently discard an already resolved item.
- [ ] Verify endpoint authorization and tenant scoping match approved Platform API conventions.
- [ ] Verify every endpoint has `module_code` and expected Arabic/English error catalogue entries.
- [ ] Verify pagination is stable/bounded if required by endpoint contract.
- [ ] Verify rate limiting matches approved requirements; do not treat rate limiting as authorization.
- [ ] Test retry stages, terminal failure, persistence, replay, discard, not-found, already-resolved, concurrent replay and tenant isolation.
- [ ] Determine whether central `platform.audit_log` is an explicit PLT-204 criterion or belongs to PLT-109/PLT-211. Do not block this ticket on later work unless detailed spec explicitly requires it; preserve PLT-204's own resolution metadata.

**Audit claims to re-check:** five-stage schedule (10s, 1m, 5m, 30m, 2h), dead-letter table and replay/discard/lag APIs reportedly exist; central audit log and metrics/alerting reportedly do not. Confirm exact retry semantics from detailed spec before changing them.

**Evidence:** retry tests, PostgreSQL dead-letter persistence, replay/discard API tests, concurrency/RLS tests and endpoint/module-code/error-catalogue evidence.

## 5. Cross-ticket integration checks

- [ ] PLT-101 Kernel primitives are consistently consumed by PLT-103/104/201/202/203.
- [ ] PLT-102 composition registers Platform API and Worker dependencies correctly.
- [ ] PLT-103 persistence conventions do not conflict with RLS/tenant session setup.
- [ ] PLT-104 tenant context is correct for API requests, background jobs and event consumers; workers must not inherit an unsafe user tenant or run unrestricted accidentally.
- [ ] PLT-201 envelope/handler/idempotency contracts match PLT-202 persistence and delivery.
- [ ] PLT-202 dispatch failures flow into PLT-204 retry/dead-letter behavior.
- [ ] PLT-203 Worker hosts job execution and outbox dispatch with correct service lifetimes.
- [ ] PLT-204 replay re-enters dispatch without losing identity/metadata or violating idempotency.
- [ ] Migrations apply from an empty PostgreSQL database in correct order.
- [ ] New schema objects, indexes, grants and RLS policies are included in migrations/bootstrap scripts.
- [ ] Existing tests and application startup remain healthy.

## 6. PostgreSQL and security proof requirements

PostgreSQL-specific behavior must be tested on PostgreSQL. Build a repeatable integration-test setup using existing Testcontainers if possible. If Docker/PostgreSQL is unavailable, do not silently skip tests and declare success; mark the relevant criterion **BLOCKED** and provide exact setup/commands.

At minimum verify:

- tenant A vs tenant B read isolation;
- no-tenant read behavior;
- raw SQL isolation using normal application role;
- cross-tenant write/update/delete isolation where applicable;
- forced RLS and role privileges;
- business mutation + outbox transaction atomicity;
- `FOR UPDATE SKIP LOCKED` concurrency;
- dispatcher role boundaries if introduced;
- job-run uniqueness and advisory locks;
- dead-letter replay transaction and concurrency.

Never use a superuser connection as proof that the application role is isolated.

## 7. Build, test and quality gates

Run supported commands and report actual results. Expected baseline gates:

```bash
dotnet build backend/Construction.sln -warnaserror
dotnet test backend/Construction.sln
```

Also run targeted tests for changed projects, PostgreSQL integration tests, architecture/contract tests if present, and migration-from-empty validation. Run frontend checks only if shared API/contract changes require them; do not expand this pass into frontend feature implementation.

Before finalizing:

```bash
git status --short --branch
git diff --check
git diff --stat
git diff
```

Report passed, failed, skipped and not-run tests separately. If Docker/PostgreSQL is unavailable, mark relevant acceptance criteria BLOCKED, not PASS.

## 8. Suggested implementation order

1. Baseline and PLT-101/102/103 verification; repair only proven gaps.
2. PLT-104 PostgreSQL RLS proof; establish test harness, verify role model and tenant behavior.
3. PLT-201 Messaging contracts; settle envelope/metadata and validation requirements against detailed specs.
4. PLT-202 outbox end-to-end delivery; real consumer registry, success/failure semantics, metadata persistence and Worker RLS access.
5. PLT-203 Worker/job conformance; timeouts, queues, persistence, locking, enablement and acceptance tests.
6. PLT-204 retry/dead-letter/replay lifecycle.
7. Cross-ticket integration, migration-from-empty and full regression.

If a blocker requires another ticket or architectural decision, stop at the boundary, document it and propose the smallest decision needed. Do not implement unrelated roadmap tickets as a workaround.

## 9. Required final deliverable from the agent

Create/update a report in the working branch, e.g. `docs/team/PLATFORM-CLOSURE-REPORT.md`, containing:

1. Starting branch, HEAD SHA, worktree status and repo version.
2. Exact Notion/repository specs consulted and any conflicts.
3. Ticket matrix for all eight tickets: every criterion, status, source path and test evidence.
4. Changed files grouped by ticket, explaining why each change was necessary.
5. Migrations/RLS/roles/grants and migration-from-empty results.
6. Integration evidence: outbox end-to-end, tenant isolation, job runner and dead-letter lifecycle.
7. Exact test commands, totals, skipped tests, environment and blockers.
8. Deferred work belonging to later tickets (e.g. PLT-105+, PLT-205+, PLT-211) and why it does not block these closures.
9. One closure recommendation per ticket and explicit remaining blockers.

Do not change Notion ticket status or claim tickets are closed. The user will review evidence and decide closure.

## 10. Definition of success

Success means all eight tickets are checked against approved acceptance criteria; every in-scope gap is implemented/tested or explicitly BLOCKED with evidence; PLT-104 isolation is proven on PostgreSQL using the real role model; PLT-202 has a working delivery path with one observable consumer effect; PLT-203 timeout/persistence/concurrency behavior is tested; PLT-204 retry/dead-letter/replay is tested; full build/test gates pass with skipped tests disclosed; and the final report provides enough evidence for a human to close each ticket confidently.

**Do not optimize for a green build at the expense of the ticket's actual Done condition.**
