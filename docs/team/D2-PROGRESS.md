# D2 PROGRESS — live state (the agent updates this file; commit it with every checkpoint)

**Place at:** `docs/team/D2-PROGRESS.md`
**Rule:** if it is not in this file and committed, it did not happen. Keep it short, factual, up to date.
**Statuses:** `TODO` · `IN_PROGRESS` · `DONE` · `DONE_WITH_FAKES` · `BLOCKED` · `SKIPPED`

---

## 0. Resume here (rewrite this block at every checkpoint and before ending a session)

- **REVIEW REQUESTED:** PLT-202 (re-status), PLT-203, PLT-204 — all three are **BLOCKED** on the environment, see §5
- **Current ticket:** PLT-204 — implementation scaffolded, unit-tested; **not provable in this environment**
- **Hard blocker (read first):** there is **no PostgreSQL** in this environment (no `psql`, no server on 5432, no
  Docker, `D2_TEST_PG` unset) and **no `docs/specs/00-*`** (see §5). Per the run rules a ticket with database
  behaviour cannot be `DONE` without real-PostgreSQL proofs, and a missing spec 00 is a stop condition.
- **Exact next action:** a human provides (1) a reachable PostgreSQL with `D2_TEST_PG`, (2) `docs/specs/00-*.md`,
  (3) `docs/team/tests/PLT-203-acceptance-spec.md` + the protected acceptance test files. Then: re-prove PLT-202 on
  real PostgreSQL, finish PLT-203 (real job-run store + advisory lock, 25 acceptance tests), then finish PLT-204.
- **Working branch:** `d2/work` · **Ticket branches:** `feat/plt-203` (`a388c18`), `feat/plt-204` (`63a6df9`)
- **Fast set:** build `-warnaserror` clean, `dotnet test --filter "Category!=Slow"` → **48/48** (all DB-free)
- **Last updated:** 2026-10-31

---

## 1. Commands discovered

| Purpose | Command |
|---|---|
| Build | `dotnet build backend/Construction.sln -warnaserror` |
| Unit + integration tests (fast set) | `dotnet test backend/Construction.sln --filter "Category!=Slow"` |
| Slow tests | `dotnet test backend/Construction.sln --filter "Category=Slow"` |
| Architecture tests | not built yet (PLT-211) |
| Contract tests | not built yet (PLT-211) |
| Tenant isolation tests | **not built yet — RLS is therefore unproven by any suite** |
| Migrations | `dotnet ef database update --project backend/src/Modules/Platform/Platform.Infrastructure --startup-project backend/src/Host/Construction.Api` (**cannot run: no database**) |
| Start dependencies | `docker compose up -d` (**cannot run: Docker is not installed**) |
| Environment | .NET SDK 10.0.401, Git 2.55. PostgreSQL: **absent** (verified 2026-10-31: `psql` not found, nothing listening on 5432, no `C:\Program Files\PostgreSQL`, `D2_TEST_PG` unset) |

## 2. Skeleton found in the repo

| Piece | Exists? | Location / notes |
|---|---|---|
| Kernel (Money, IClock, ids) | Yes | `backend/src/Shared/Construction.Kernel` |
| Messaging / Outbox base | Implemented | `backend/src/Shared/Construction.Messaging` (PLT-201) + `EfOutboxWriter` (PLT-202) |
| Persistence + RLS + tenant context | Yes | `backend/src/Shared/Construction.Persistence` |
| Web (auth, problem+json, idempotency) | Empty | `backend/src/Shared/Construction.Web` |
| **Testing helpers + shared PostgreSQL fixture** | **Empty** | `backend/src/Shared/Construction.Testing` — no fixture was ever written, so nothing can talk to `D2_TEST_PG` |
| Architecture.Tests / module template | Empty | `backend/tests/Architecture.Tests`, `backend/tools/templates/module` |
| Job library | None | hand-written runner (ADR-002) |
| **`docs/specs/`** | **Only `PREF/`** | The specs were relocated to `PREF/` and contain **01, 02, 04, 05, 06, 07, 10 only — `00-*` and `20-*` are missing** |
| **`docs/team/tests/PLT-203-acceptance-spec.md`** | **Missing** | Also missing: `IJobHarness.cs`, `JobRunnerConformanceTests.cs`, `RealJobRunnerConformanceTests.cs` (the protected 25-test package) |

---

## 3. Queue

| # | Ticket | Status | Last commit / tag | Tests (pass/total) | Notes |
|---|---|---|---|---|---|
| 1 | PLT-201 Messaging library | DONE | `d2-PLT-201-done` | 18/18 | Shared library; no database behaviour required |
| 2 | PLT-202 Outbox | DONE_WITH_FAKES | tag `d2-PLT-202-done` (code) — see §4 | 31 fast + 1 Slow, all **in-memory** | Code complete; **no real-PostgreSQL proof**; required RFC was missing (now written) |
| 3 | PLT-206 Number series | TODO | | | Not started (out of scope this run) |
| 4 | PLT-203 Worker + job runner | BLOCKED (environment) | `feat/plt-203` (code committed) | 7 unit tests; **0 of the 25 required acceptance tests exist** | Real `job_runs` store + advisory lock not wired; see §4 |
| 5 | PLT-205 Idempotency-Key | TODO | | | Deliberately skipped this run |
| 6 | PLT-204 Retries, dead-letter, replay | BLOCKED (environment) | `feat/plt-204` (wip) | 10 new unit tests green; **all PostgreSQL proofs missing** | See §4 |
| 7 | PLT-211 CI/CD gates | TODO | | | |
| 8 | PLT-207 Files | TODO | | | |
| 9 | PLT-208 Approvals engine | TODO | | | |
| 10 | PLT-209 Notifications | TODO | | | |
| 11 | PLT-210 Excel import framework | TODO | | | |
| 12 | CST-201 Budget | TODO | | | |
| 13 | CST-206 budget.approved.v1 | TODO | | | |
| 14 | CST-205 Period tables + guard + close/reopen | TODO | | | |
| 15 | CST-202 Cost ledger | TODO | | | |
| 16 | CST-203 Cost consumers | TODO | | | |
| 17 | CST-204 Earned value | TODO | | | |
| 18 | CSH-201 Schedules from events | TODO | | | |
| 19 | CSH-202 13-week forecast | TODO | | | |

---

## 4. Ticket log

### PLT-201 — Messaging library
- **Status:** DONE · **Branch:** `feat/plt-201` · **Tests:** 18/18
- Envelope, `IOutbox`, `EventHandler`, idempotent consumer, schema helpers. `Messaging` references only `Kernel`.
- **Spec rules covered:** 01 §4.5 `platform.processed_events` (PK `(consumer, event_id)`, written in the consumer's
  transaction); Brief §2.5 envelope.
- **Defaults used:** envelope field set per Brief §2.5; DB access behind `IProcessedEventStore`.

### PLT-202 — Outbox
- **Status:** **DONE_WITH_FAKES** (was recorded `DONE`; corrected 2026-10-31)
- **Branch:** `feat/plt-202` · **Tag:** `d2-PLT-202-done` (kept — history is not rewritten)
- **Why the correction:** every PLT-202 test (*rollback ⇒ no row, commit ⇒ one row, N dispatchers, crash/redelivery,
  per-aggregate order, lag metric, 5 000-event drain*) runs against `InMemoryOutboxRepository` /
  `InMemoryProcessedEventStore` / EF-InMemory. The recipe and the run rules require these on **real PostgreSQL**;
  in-memory fakes are not proof of RLS, `FOR UPDATE SKIP LOCKED`, partial indexes or `jsonb` behaviour.
- **Spec rules covered (design):** 01 §4.5 `platform.outbox` (append-only + dispatch bookkeeping, per-aggregate
  sequence, partial indexes), 01 §4.14 (job/worker split), BR-PLT-60 (same-transaction write); `platform.processed_events`
  from 01 §4.5/BR-PLT-62 for effect-once.
- **Deliberate deviation from 01 §4.5 (recorded, §8 #3):** the spec models fan-out (`outbox_subscriptions` +
  `outbox_deliveries`, per-consumer attempt state); PLT-202 implements a single dispatch state per outbox row, as the
  recipe specifies. Per-consumer retry independence is therefore **not** implemented.
- **New artefact this run:** `docs/rfc/0001-outbox-dispatcher-role.md` (was required by PLT-202 and missing).
- **Needs review (lane):** `backend/src/Shared/Construction.Persistence/{OutboxMessage,EfOutboxWriter}.cs`,
  `Construction.Persistence.csproj`, `audit/db/expectations.yaml`.
- **Replace later (required):**
  1. `InMemoryOutboxRepository` → real `PostgresOutboxRepository` against `D2_TEST_PG` for all 8 required tests.
  2. `InMemoryProcessedEventStore` → `DbProcessedEventStore` on real PostgreSQL.
  3. EF-InMemory `PlatformDbContext` → Npgsql provider, app role, RLS enabled and forced.
  4. Cross-tenant claim: needs the `cc_outbox_dispatcher` role from RFC 0001 (**not creatable in the agent's lane**).
  5. Outbox retention as a job — scheduled through the PLT-203 runner, pending a real job-run store.

### PLT-203 — Worker host and job runner
- **Status:** **BLOCKED (environment)** (was recorded `DONE`; corrected 2026-10-31)
- **Branch:** `feat/plt-203` · **Commit:** `a388c18` (verified: that commit alone builds `-warnaserror` and passes 38/38) · **Tests:** 7 unit tests green (`JobRunnerTests`); **required package absent**
- **Why blocked:**
  1. The required acceptance package does not exist in the repo: `docs/team/tests/PLT-203-acceptance-spec.md`,
     `IJobHarness.cs`, `JobRunnerConformanceTests.cs`, `RealJobRunnerConformanceTests.cs`. The run requires
     "all 25 tests pass" by implementing only `JobHarness.cs`; there is nothing to implement against.
  2. `platform.job_runs` persistence and the `pg_try_advisory_lock` semantics cannot be exercised (no PostgreSQL).
  3. **Production wiring defect found:** `PlatformInfrastructureRegistration.AddPlatformWorkerJobs` registers
     `IJobRunStore → InMemoryJobRunStore` and `IJobLock → InMemoryJobLock`. `PostgresJobLock` exists but is unused and
     there is **no Postgres job-run store at all**, so the Worker would never write `platform.job_runs` and the
     advisory lock would be per-process only (two Worker instances would both run the job).
- **Implemented:** Worker host + `AppModules` switches, `IJob`/`JobContext`, queues
  (`critical|default|reports|collectors|ai`), per-tenant enablement, hard timeout via linked token, `platform.job_runs`
  migration + RLS, scheduler loop, outbox dispatch hosted service, ADR-002, `PlatformHeartbeatJob`.
- **Spec rules covered:** 01 §4.14 `platform.job_runs` (`job_code, run_key, trigger, status CHECK
  queued|running|succeeded|failed|timed_out|skipped|cancelled, attempts, error`), per-tenant enablement
  (`job_tenant_settings`), advisory locking keyed by `(job_code, tenant_id)`; BR-PLT-90-range job rules via 01 §4.14.
- **Cron:** numeric `"M H * * *"` only, evaluated in the tenant time zone; any other expression throws
  `NotSupportedException`. No cron package added.
- **Needs review (lane):** `backend/src/Host/Construction.Worker/**` (`AppModules.cs`, `Program.cs`,
  `appsettings.json`, `JobSchedulerHostedService.cs`, `OutboxDispatcherHostedService.cs`), `docs/adr/ADR-002-job-runner.md`,
  `audit/db/expectations.yaml`.
- **Replace later (required):** the in-memory job-run store and lock (see defect 3) and the 25 protected acceptance tests.

### PLT-204 — Retries, dead-letter, replay
- **Status:** **BLOCKED (environment + spec missing)** — partial implementation only
- **Branch:** `feat/plt-204` · **Commit:** `63a6df9` (wip, **not merged, no tag**) · **Tests:** 10 new unit tests (`DeadLetterRetryTests`), 48/48 fast set green
- **Implemented so far:**
  - Exact backoff schedule `attempt 1 → +10 s, 2 → +1 m, 3 → +5 m, 4 → +30 m, 5 → +2 h`, then `dead_at` on the 5th
    failure (recipe wording; see §8 #4 for the discrepancy with BR-PLT-63).
  - `platform.outbox_dead` table + forced RLS + `(tenant_id, dead_at)` index; dead-letter copy written in the same
    transaction as the failure bookkeeping; category + bounded summary only (no payload, no exception message).
  - `GET /api/v1/platform/outbox/dead` (keyset pagination, `limit` 1-100) and
    `POST /api/v1/platform/outbox/dead/{id}/replay` (conditional `UPDATE … WHERE replayed_at IS NULL`, always 200
    only on the winner, otherwise 409; inserts a fresh outbox row with `attempts = 0`).
  - Rate limiting (10/min per user) on replay; audit captured as `replayed_by` + `replay_reason` on the dead row.
  - **Fixed this run:** permission codes. The recipe says `platform.outbox.replay`; spec 01 §10.2 and §12 say
    `platform.outbox.view` (list/get) and `platform.outbox.manage` (replay/discard). The scaffold used the recipe's
    non-existent code; it now uses the spec's.
- **Not done / blocked:** the whole PostgreSQL proof set (5th failure ⇒ dead row + metric; replay ⇒ one effect;
  replay twice ⇒ 409; E1-E15; audit row) and the "dead-letter count" metric/alert (no metrics pipeline exists yet).
  `platform.audit_log` is not written on replay because no audit writer exists in the codebase yet (PLT-211 scope);
  the dead row's `replayed_by`/`replay_reason` is the interim record — recorded as a follow-up.
- **Spec rules covered (design):** 01 §4.5 `platform.outbox_dead` (attempts, error class/message, resolution,
  resolved_by/at/note), BR-PLT-63 (retry schedule then dead letter + alert), BR-PLT-64 (replay re-queues with attempts
  reset), 01 §12 API table (GET `/outbox/dead` → `platform.outbox.view`; POST `{id}/replay` → `platform.outbox.manage`),
  §10.2 permission catalogue.
- **Needs review (lane):** `backend/src/Host/Construction.Api/{Program.cs,AppModules.cs}`, `audit/api/scenarios.yaml`.
- **Replace later (required):** every item in the blocked list above.

---

## 5. Blocked

| Ticket | Since | What is blocked and why | What was tried | What is needed and from whom |
|---|---|---|---|---|
| PLT-202, PLT-203, PLT-204 | 2026-10-31 | **No PostgreSQL.** No `psql`/server/5432 listener, no Docker, `D2_TEST_PG` unset, `Construction.Testing` empty (no shared fixture). Every DB-behaviour requirement is unprovable. | Checked `psql`, `pg_ctl`, Windows services, listeners on 5432-5437, env vars, Docker. | A reachable PostgreSQL + `D2_TEST_PG` + the shared fixture in `Construction.Testing` (architect/D1). |
| PLT-202, PLT-203, PLT-204 | 2026-10-31 | **`docs/specs/00-backend-architecture-and-conventions.md` is missing** (specs now live in `PREF/`; `PREF/README.md` lists 00, 03, 08, 09, 20 as never exported). Rule numbers and conventions in 00 cannot be verified. | Searched the whole repo for `00-*`; read `PREF/README.md`. | Export 00 (and 20) from Notion into `PREF/` (human). |
| PLT-203 | 2026-10-31 | **Acceptance package absent:** `docs/team/tests/PLT-203-acceptance-spec.md` + the 4 protected test files. | Listed `docs/team/` and the test project. | Provide the package (architect); then implement `JobHarness.cs` only. |
| PLT-202, PLT-204 | 2026-10-31 | **Cross-tenant dispatcher role** for `platform.outbox` under forced RLS; `deploy/` is review-flagged and no role can be created by the agent. | Wrote `docs/rfc/0001-outbox-dispatcher-role.md` with a table-scoped policy; kept the code behind a connection factory. | Architect approval + `deploy/docker/initdb/01-roles-schemas.sql` change (PLT-211). |

---

## 6. Decisions taken

| # | Decision | Default or new | Ticket | Needs confirmation from |
|---|---|---|---|---|
| 1 | EventEnvelope per Brief §2.5 / 01 §4.5 | Default | PLT-201 | Architect |
| 2 | `Messaging` references only `Kernel`; DB behind `IProcessedEventStore` | New | PLT-201 | Architect |
| 3 | Per-aggregate eligibility: an earlier undispatched **or dead** row blocks later events of the aggregate | Default (Runbook §6 #2) | PLT-202 | Architect |
| 4 | Single dispatch state per outbox row instead of spec 01 §4.5 fan-out (`outbox_deliveries`) | New | PLT-202 | Architect — see §8 #3 |
| 5 | Claim batch 50, lease 60 s, retention 14 d | Default | PLT-202 | Architect |
| 6 | Dead-letter + replay use spec 01 §10.2 codes `platform.outbox.view` / `platform.outbox.manage` (**not** the recipe's `platform.outbox.replay`) | New (verified) | PLT-204 | Architect |
| 7 | Dead-letter on the **5th** failure (recipe wording), so the `+2 h` step is recorded but not waited | Default | PLT-204 | Architect — see §8 #4 |
| 8 | Replay audit is `replayed_by` + `replay_reason` on the dead row until a real `IAuditWriter` exists | New | PLT-204 | Architect |
| 9 | Hand-written job runner, no new package (ADR-002) | Default (Runbook §6 #3) | PLT-203 | Architect |

## 7. Follow-ups (not in current tickets)

| Item | Found in | Suggested owner |
|---|---|---|
| Postgres `IJobRunStore` + wire `PostgresJobLock` (currently in-memory in production wiring) | PLT-203 | PLT-203 follow-up |
| `platform.audit_log` writer + audit row on replay (S10) | PLT-204 | PLT-211 |
| `platform.audit_log` table does not exist yet | PLT-204 | PLT-211 |
| Dead-letter count metric + alert (`outbox_dead`) | PLT-204 | PLT-211 (observability) |
| `POST /outbox/dead/{id}/discard` (spec 01 §12, needs `resolution_note`) | PLT-204 | PLT-204 follow-up |
| Test-only dead-letter seeder `POST /test/outbox/dead` required by `audit/api/scenarios.yaml` | PLT-204 | PLT-211 |
| `platform.outbox_dead` column set differs from spec 01 §4.5 (`resolution`, `error_class`, `stack_trace`, `resolved_*` missing) | PLT-204 | Architect decision (§8 #5) |
| Retention of `outbox_dead` (BR-PLT-66: resolved + 1 year) | PLT-204 | PLT-204 follow-up |
| `processed_events` 30-day purge job | PLT-202/204 | PLT-204 follow-up |

## 8. Open questions for the architect / product owner

| # | Question | Blocks | Default in use |
|---|---|---|---|
| 1 | Export `00-backend-architecture-and-conventions.md` and `20-repository-structure.md` into `PREF/` | verifying conventions and rule numbers | 01 + Brief + recipes only |
| 2 | Exact worker DB role name / grants for cross-tenant outbox claim | PLT-202/204 proof | RFC 0001 (`cc_outbox_dispatcher`) |
| 3 | Spec 01 §4.5 fans out per consumer (`outbox_subscriptions` + `outbox_deliveries`); the recipe implements one dispatch state per row. Which is authoritative? | PLT-202 contract, PLT-204 retries | recipe (single state) |
| 4 | Dead-letter after the **5th** failure (recipe/prompt) or after the 5 retries are exhausted, i.e. the 6th (BR-PLT-63 "10 s, 1 m, 5 m, 30 m, 2 h, then outbox_dead")? | PLT-204 schedule test | 5th (recipe) |
| 5 | `platform.outbox_dead` shape: spec has `resolution`/`error_class`/`stack_trace`/`resolved_*`; the scaffold has `replayed_at`/`replayed_by`/`replay_reason` + aggregate columns | PLT-204 schema | scaffold (recipe columns + aggregate) |
| 6 | Tenant rule for the dead-letter list: tenant-scoped RLS (current) or an explicitly cross-tenant operator view (spec 01 §12 mentions `platform_operator`)? | PLT-204 cross-tenant semantics | tenant-scoped |

## 9. Session log

| Date | What was completed | Ended at |
|---|---|---|
| 2026-10-30 | Bootstrap; PLT-201 messaging library 18/18 green | PLT-201 done |
| 2026-10-30 | PLT-202 outbox schema/writer/dispatcher/metrics; 31 fast + 1 Slow (in-memory) | PLT-202 tagged |
| 2026-10-30 | PLT-203 worker host, job runner, job_runs migration, 7 unit tests | PLT-203 implementation |
| 2026-10-31 | Audit of the above: found no PostgreSQL, no spec 00, no PLT-203 acceptance package, in-memory production wiring; re-statused PLT-202/203; added PLT-204 unit tests, RFC 0001, audit scenarios, permission fix; committed 203 and 204-wip | BLOCKED — awaiting environment + specs |
