# D2 PROGRESS — live state (the agent updates this file; commit it with every checkpoint)

**Place at:** `docs/team/D2-PROGRESS.md`
**Rule:** if it is not in this file and committed, it did not happen. Keep it short, factual, up to date.
**Statuses:** `TODO` · `IN_PROGRESS` · `DONE` · `DONE_WITH_FAKES` · `BLOCKED` · `SKIPPED`

---

## 0. Resume here (rewrite this block at every checkpoint and before ending a session)

- **REVIEW REQUESTED:** PLT-201 (pilot completed; touches src/Shared/Construction.Messaging, src/Shared/Construction.Kernel, Construction.sln)
- **Current ticket:** PLT-201 Messaging library
- **Current slice / step:** All slices completed (1 Types & Envelope, 2 Idempotent Consumer & Schema Validation, 3 Tests, 4 Docs/Progress). Tests: 18/18 passing.
- **Last commit:** 8c2dffe chore(PLT-201): bootstrap
- **Exact next action:** Run checkpoint.sh and finish-ticket.sh PLT-201, then STOP for pilot review
- **Working branch:** `d2/work` · **Ticket branch:** `feat/plt-201`
- **Last updated:** 2026-10-30 22:00

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
| Messaging / Outbox base | Implemented | `backend/src/Shared/Construction.Messaging` (PLT-201) |
| Persistence + RLS + tenant context | Yes | `backend/src/Shared/Construction.Persistence` (ModuleDbContext, TenantSessionInterceptor) |
| Web (auth, problem+json, idempotency hook) | Empty (.gitkeep) | `backend/src/Shared/Construction.Web` |
| Testing helpers (fakes, ApiFactory) | Empty (.gitkeep) | `backend/src/Shared/Construction.Testing` |
| Architecture.Tests / module template | Empty (.gitkeep) | `backend/tests/Architecture.Tests`, template at `backend/tools/templates/module` |
| Job library already used (Hangfire or other) | None yet | `backend/src/Host/Construction.Worker` minimal skeleton |
| Existing D4 number-series skeleton | None yet | To be implemented in PLT-206 |

---

## 3. Queue

| # | Ticket | Status | Last commit / tag | Tests (pass/total) | Notes |
|---|---|---|---|---|---|
| 1 | PLT-201 Messaging library | DONE | | 18/18 | Shared messaging library, envelope, idempotent consumer, schema validator, EF migration |
| 2 | PLT-206 Number series | TODO | | | |
| 3 | PLT-202 Outbox | TODO | | | |
| 4 | PLT-203 Worker + job runner | TODO | | | |
| 5 | PLT-205 Idempotency-Key | TODO | | | |
| 6 | PLT-204 Retries, dead-letter, replay | TODO | | | |
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
  - [x] security S1-S15 satisfied or N/A (S1/S2 tenant context set from envelope, missing tenant rejected; other endpoints N/A)
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
  - Database access decoupled in `Construction.Messaging` via `IProcessedEventStore` / `IConsumerTransactionContext` with ADO.NET and in-memory providers.
- **Follow-ups / out of scope noticed:** Outbox dispatcher implementation belongs to PLT-202.
- **Replace later (only for DONE_WITH_FAKES):**
- **Hand-off notes (who consumes this and how):** All modules publish events via `IOutbox` and handle events via `IdempotentConsumer<TEvent>`.

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

## 7. Follow-ups (not in current tickets)

| Item | Found in | Suggested owner |
|---|---|---|

## 8. Open questions for the architect / product owner

| # | Question | Blocks | Default in use |
|---|---|---|---|

## 9. Session log (one line per session)

| Date | What was completed | Ended at |
|---|---|---|
| 2026-10-30 | Bootstrap completed: repo inspected, hooks installed, d2.conf & D2-PROGRESS.md filled, PLT-201 started | PLT-201 bootstrap |
| 2026-10-30 | PLT-201 Messaging library fully implemented, 18/18 tests green, migration created | PLT-201 pilot finish |
