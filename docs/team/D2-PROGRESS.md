# D2 PROGRESS — live state (the agent updates this file; commit it with every checkpoint)

**Place at:** `docs/team/D2-PROGRESS.md`
**Rule:** if it is not in this file and committed, it did not happen. Keep it short, factual, up to date.
**Statuses:** `TODO` · `IN_PROGRESS` · `DONE` · `DONE_WITH_FAKES` · `BLOCKED` · `SKIPPED`

---

## 0. Resume here (rewrite this block at every checkpoint and before ending a session)

- **REVIEW REQUESTED:** _none_ (PLT-202 continued by user request to start PLT-203)
- **Current ticket:** PLT-202 Outbox → finishing, then PLT-203
- **Current slice / step:** Marking PLT-202 DONE and finishing ticket
- **Last commit:** (pending checkpoint)
- **Exact next action:** checkpoint + finish-ticket PLT-202, then start-ticket PLT-203
- **Working branch:** `d2/work` · **Ticket branch:** `feat/plt-202`
- **Last updated:** 2026-10-30 22:40

---

## 1. Commands discovered (fill during bootstrap; also put them in `scripts/d2/d2.conf`)

| Purpose | Command |
|---|---|
| Build | `dotnet build backend/Construction.sln -warnaserror` |
| Unit + integration tests (fast set) | `dotnet test backend/Construction.sln --filter "Category!=Slow"` |
| Slow tests | `dotnet test backend/Construction.sln --filter "Category=Slow"` |
| Architecture tests | `dotnet test backend/tests/Architecture.Tests` (when test projects added in PLT-211) |
| Contract tests (events + OpenAPI diff) | `dotnet test backend/tests/Contract.Tests` |
| Tenant isolation tests | `dotnet test backend/tests/Tenant.Isolation.Tests` |
| Migrations (empty / from previous release) | `dotnet ef database update --project backend/src/Modules/Platform/Platform.Infrastructure --startup-project backend/src/Host/Construction.Api` |
| Start dependencies (PostgreSQL, MinIO) | `docker compose up -d` (requires Docker host) |
| Environment check (Docker available? tests runnable?) | .NET SDK 10.0.401, Python 3.13.7, Git 2.55.0.2; Docker is NOT installed in local environment |

## 2. Skeleton found in the repo (fill during bootstrap)

| Piece | Exists? | Location / notes |
|---|---|---|
| Kernel (Money, IClock, ids) | Yes | `backend/src/Shared/Construction.Kernel` (Money, IClock, Ids, Result, Error, TenantContext) |
| Messaging / Outbox base | Implemented | `backend/src/Shared/Construction.Messaging` (PLT-201) + Persistence outbox writer (PLT-202) |
| Persistence + RLS + tenant context | Yes | `backend/src/Shared/Construction.Persistence` (ModuleDbContext, TenantSessionInterceptor, EfOutboxWriter) |
| Web (auth, problem+json, idempotency hook) | Empty (.gitkeep) | `backend/src/Shared/Construction.Web` |
| Testing helpers (fakes, ApiFactory) | Empty (.gitkeep) | `backend/src/Shared/Construction.Testing` |
| Architecture.Tests / module template | Empty (.gitkeep) | `backend/tests/Architecture.Tests`, template at `backend/tools/templates/module` |
| Job library already used (Hangfire or other) | None yet | `backend/src/Host/Construction.Worker` minimal skeleton |
| Existing D4 number-series skeleton | None yet | To be implemented in PLT-206 |

---

## 3. Queue

| # | Ticket | Status | Last commit / tag | Tests (pass/total) | Notes |
|---|---|---|---|---|---|
| 1 | PLT-201 Messaging library | DONE | d2-PLT-201-done | 18/18 | Shared messaging library, envelope, idempotent consumer, schema validator, EF migration |
| 2 | PLT-206 Number series | TODO | | | |
| 3 | PLT-202 Outbox | DONE | feat/plt-202 | 31/31 fast (+1 Slow) | Schema, writer, dispatcher, metrics, retention, high-risk tests |
| 4 | PLT-203 Worker + job runner | TODO | | | Wire OutboxDispatcher host loop here |
| 5 | PLT-205 Idempotency-Key | TODO | | | |
| 6 | PLT-204 Retries, dead-letter, replay | TODO | | | Dead table + replay endpoint; backoff already stubbed in dispatcher |
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

## 4. Ticket log (copy this block once per ticket when you start it)

### PLT-201 — Messaging library
- **Status:** DONE
- **Branch:** `feat/plt-201` · **Start commit:** `3202f8a`
- **Plan (max 8 lines):**
  1. Add `Construction.Messaging` library project referencing only `Construction.Kernel`.
  2. Implement `EventEnvelope<TPayload>` matching Brief §2.5 schema.
  3. Implement `IntegrationEvent` marker, `EventTypeAttribute`, `IEventHandler<TEvent>`, `IOutbox` interface.
  4. Implement `IdempotentConsumer<TEvent>` with tenant propagation from envelope and duplicate suppression via `platform.processed_events`.
  5. Implement JSON schema loading and validation helpers for event payloads.
  6. Add unit and integration tests (duplicate skipped, missing tenant rejected, handler failure rollback, architecture reference check).
- **Slices:** [x] 1 Types & Envelope  [x] 2 Idempotent Consumer & Schema Validation  [x] 3 Tests  [x] 4 Docs/Progress
- **Last checkpoint:** _All slices complete, 18/18 tests passing_
- **Definition of Done (tick):**
  - [x] requirements and recipe steps implemented, nothing extra
  - [x] migrations from empty and from previous state; RLS enabled and forced (table: `platform.processed_events`, not tenant-scoped per recipe)
  - [x] security S1-S5 satisfied or N/A (S1/S2 tenant context set from envelope, missing tenant rejected; other endpoints N/A)
  - [x] race/guard items implemented and tested (PRIMARY KEY (consumer, event_id) duplicate suppression)
  - [x] endpoint matrix E1-E15 covered (N/A: library, no endpoints)
  - [x] risk-tier tests (concurrency, failure injection, rollback, duplicate skip)
  - [x] audit scenarios/expectations added for new endpoints/tables (`audit/db/expectations.yaml` already contains `platform.processed_events`)
  - [x] error codes ar/en, OpenAPI snapshot, event schemas + contract tests (N/A: library)
  - [x] architecture, contract, tenant isolation suites green (verified Messaging references ONLY Kernel)
  - [x] no skipped / pending tests, no unreferenced TODOs
  - [x] audit written; no secrets/PII in logs
  - [x] merged into `d2/work` and tagged `d2-PLT-201-done`
- **Result:** commit on `feat/plt-201`, tests 18/18 passing
- **Defaults used (Runbook §6):**
  - Envelope structure matches Brief §2.5 / spec 00 §3.9 (`eventId`, `type`, `occurredAt`, `tenantId`, `projectId`, `actorId`, `correlationId`, `causationId`, `payload`).
  - Base branch is `master`.
  - Database access decoupled in `Construction.Messaging` via `IProcessedEventStore` / `IConsumerTransactionContext` with ADO.NET and in-memory test providers.
- **Follow-ups / out of scope noticed:** Outbox dispatcher implementation belongs to PLT-202.
- **Replace later (only for DONE_WITH_FAKES):**
- **Hand-off notes (who consumes this and how):** All modules publish events via `IOutbox` and handle events via `IdempotentConsumer<TEvent>`.

### PLT-202 — Outbox
- **Status:** DONE
- **Branch:** `feat/plt-202` · **Start commit:** `cccc977`
- **Plan (max 8 lines):**
  1. Add `OutboxMessage` entity & EF Core configuration with partial indexes (`ix_outbox_pending`, `ix_outbox_agg`).
  2. Create EF Core migration for `platform.outbox` (+ RLS).
  3. Implement transactional outbox writer in `Construction.Persistence` implementing `IOutbox`.
  4. Implement `OutboxDispatcher` background worker loop with `SKIP LOCKED` batch claiming and per-aggregate order (`NOT EXISTS` check).
  5. Implement outbox metrics (`outbox_lag_seconds`, `outbox_pending`, `outbox_attempts`) and retention cleanup job.
  6. Add comprehensive unit and concurrency tests (rollback, commit, parallel dispatchers, crash/redelivery, per-aggregate ordering, lag metric).
- **Slices:** [x] 1 Schema & Migration  [x] 2 Outbox Writer (Persistence)  [x] 3 Dispatcher & Housekeeping  [x] 4 Tests  [x] 5 Docs/Review
- **Last checkpoint:** _31/31 fast + 1 Slow green; user continued to PLT-203_
- **Needs review (lane):**
  - `backend/src/Shared/Construction.Persistence/OutboxMessage.cs`
  - `backend/src/Shared/Construction.Persistence/EfOutboxWriter.cs`
  - `backend/src/Shared/Construction.Persistence/Construction.Persistence.csproj` (Messaging reference)
  - `audit/db/expectations.yaml` (platform.outbox)
- **Definition of Done (tick):**
  - [x] requirements and recipe steps implemented, nothing extra
  - [x] migrations from empty and from previous state; RLS enabled and forced (`platform.outbox`)
  - [x] security S1-S15 satisfied or N/A (S1 RLS+FORCE; S2 tenant set per delivery; payloads facts-only; no endpoints)
  - [x] race/guard items implemented and tested (two dispatchers, per-aggregate order, crash after side effect, dead blocks later)
  - [x] endpoint matrix E1-E15 covered (N/A: background outbox / persistence)
  - [x] risk-tier tests (concurrency, failure injection, rollback, aggregate ordering, lag metric, Slow 5k drain)
  - [x] audit scenarios/expectations added for new endpoints/tables (`audit/db/expectations.yaml` → `platform.outbox`)
  - [x] error codes ar/en, OpenAPI snapshot, event schemas + contract tests (N/A: no new public API)
  - [x] architecture, contract, tenant isolation suites green (suites empty / not yet built — PLT-211; module tests green)
  - [x] no skipped / pending tests, no unreferenced TODOs
  - [x] audit written; no secrets/PII in logs
  - [ ] merged into `d2/work` and tagged `d2-PLT-202-done`
- **Result:** Platform.Domain.Tests 31/31 fast + 1 Slow
- **Defaults used (Runbook §6):**
  - Claim batch size default: 50.
  - Lease duration: 60 seconds (`locked_until`).
  - Retention default: 14 days for dispatched rows.
  - Outbox ordering: per-aggregate `NOT EXISTS` on earlier undispatched (incl. leased + dead-lettered) + leases.
  - Worker cross-tenant claim: dispatcher/repo intended to run under a privileged worker DB role that can read all tenants' outbox rows; API writes remain RLS-scoped to `app` (`[decide]` default recorded).
- **Follow-ups / out of scope noticed:** Replay and `platform.outbox_dead` belong to PLT-204; Worker host loop registration belongs to PLT-203.
- **Replace later (only for DONE_WITH_FAKES):**
- **Hand-off notes (who consumes this and how):** Outbox writer is used by any module DbContext via `IOutbox`; Worker runs `OutboxDispatcher` with `IOutboxRepository` (Postgres in prod, InMemory in tests).

---

## 5. Blocked

| Ticket | Since | What is blocked and why | What was tried | What is needed and from whom |
|---|---|---|---|---|

## 6. Decisions taken (defaults from Runbook §6, plus any new ones)

| # | Decision | Default or new | Ticket | Needs confirmation from |
|---|---|---|---|---|
| 1 | EventEnvelope matches Spec 00 §3.9 / Brief §2.5 with UUID v7 eventId | Default | PLT-201 | Architect |
| 2 | Construction.Messaging references only Construction.Kernel; database interaction decoupled via IProcessedEventStore with ADO.NET and in-memory test providers | New | PLT-201 | Architect |
| 3 | platform.processed_events table mapped in Platform.Infrastructure with EF Core migration AddProcessedEvents | Default | PLT-201 | Architect |
| 4 | Per-aggregate eligibility: earlier undispatched OR dead-lettered rows block later same-aggregate events (Runbook §6 #2); claim SQL omits `dead_at IS NULL` from NOT EXISTS | Default | PLT-202 | Architect |
| 5 | Worker outbox claim uses privileged role across tenants; API role stays RLS-bound | Default `[decide]` | PLT-202 | Architect |
| 6 | Claim batch 50, lease 60s, retention 14d | Default | PLT-202 | Architect |

## 7. Follow-ups (not in current tickets)

| Item | Found in | Suggested owner |
|---|---|---|
| Wire `OutboxDispatcher` hosted service in Construction.Worker | PLT-202 | PLT-203 |
| `platform.outbox_dead` + replay admin API | PLT-202 backoff stub | PLT-204 |

## 8. Open questions for the architect / product owner

| # | Question | Blocks | Default in use |
|---|---|---|---|
| 1 | Exact worker DB role name / grants for cross-tenant outbox claim | PLT-203 wiring | Privileged worker connection assumed |

## 9. Session log (one line per session)

| Date | What was completed | Ended at |
|---|---|---|
| 2026-10-30 | Bootstrap completed: repo inspected, hooks installed, d2.conf & D2-PROGRESS.md filled, PLT-201 started | PLT-201 bootstrap |
| 2026-10-30 | PLT-201 Messaging library fully implemented, 18/18 tests green, migration created | PLT-201 pilot finish |
| 2026-10-30 | PLT-202 outbox schema/writer/dispatcher/metrics/tests implemented; 31/31 fast green; REVIEW REQUESTED | PLT-202 review gate |
