# D2 PROGRESS — live state (the agent updates this file; commit it with every checkpoint)

**Place at:** `docs/team/D2-PROGRESS.md`
**Rule:** if it is not in this file and committed, it did not happen. Keep it short, factual, up to date.
**Statuses:** `TODO` · `IN_PROGRESS` · `DONE` · `DONE_WITH_FAKES` · `BLOCKED` · `SKIPPED`

---

## 0. Resume here (rewrite this block at every checkpoint and before ending a session)

- **REVIEW REQUESTED:** _none_
- **Current ticket:** _none yet_
- **Current slice / step:** _n/a_
- **Last commit:** _n/a_
- **Exact next action:** _read the Runbook, run the bootstrap (Runbook §1.6), then start queue item 1_
- **Working branch:** `d2/work` · **Ticket branch:** _n/a_
- **Last updated:** _date/time_

---

## 1. Commands discovered (fill during bootstrap; also put them in `scripts/d2/d2.conf`)

| Purpose | Command |
|---|---|
| Build | |
| Unit + integration tests (fast set) | |
| Slow tests | |
| Architecture tests | |
| Contract tests (events + OpenAPI diff) | |
| Tenant isolation tests | |
| Migrations (empty / from previous release) | |
| Start dependencies (PostgreSQL, MinIO) | |
| Environment check (Docker available? tests runnable?) | |

## 2. Skeleton found in the repo (fill during bootstrap)

| Piece | Exists? | Location / notes |
|---|---|---|
| Kernel (Money, IClock, ids) | _expected: yes_ | `backend/src/Shared/Construction.Kernel` |
| Messaging / Outbox base | _expected: empty (.gitkeep)_ | `backend/src/Shared/Construction.Messaging` |
| Persistence + RLS + tenant context | _expected: ModuleDbContext, TenantSessionInterceptor_ | `backend/src/Shared/Construction.Persistence` |
| Web (auth, problem+json, idempotency hook) | _expected: empty_ | `backend/src/Shared/Construction.Web` |
| Testing helpers (fakes, ApiFactory) | _expected: empty_ | `backend/src/Shared/Construction.Testing` |
| Architecture.Tests / module template | _expected: empty_ | `backend/tests/Architecture.Tests`, `backend/tools/templates/module` |
| Job library already used (Hangfire or other) | | |
| Existing D4 number-series skeleton | | |

---

## 3. Queue

| # | Ticket | Status | Last commit / tag | Tests (pass/total) | Notes |
|---|---|---|---|---|---|
| 1 | PLT-201 Messaging library | TODO | | | |
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

### <TICKET-ID> — <title>
- **Status:** IN_PROGRESS
- **Branch:** `feat/<id>` · **Start commit:** `<sha>`
- **Plan (max 8 lines):**
- **Slices:** [ ] 1 Schema  [ ] 2 Domain  [ ] 3 Application  [ ] 4 Persistence/Integration  [ ] 5 API  [ ] 6 Risk tests  [ ] 7 Docs
- **Last checkpoint:** _slice N done, next: slice N+1_
- **Definition of Done (tick):**
  - [ ] requirements and recipe steps implemented, nothing extra
  - [ ] migrations from empty and from previous state; RLS enabled and forced
  - [ ] security S1-S15 satisfied or N/A (list N/A items)
  - [ ] race/guard items implemented and tested
  - [ ] endpoint matrix E1-E15 covered
  - [ ] risk-tier tests (concurrency, failure injection, property, golden, load smoke)
  - [ ] audit scenarios/expectations added for new endpoints/tables (`audit/api/scenarios.yaml`, `audit/db/expectations.yaml`); `AUDIT_CMD` passes if set
  - [ ] error codes ar/en, OpenAPI snapshot, event schemas + contract tests
  - [ ] architecture, contract, tenant isolation suites green
  - [ ] no skipped / pending tests, no unreferenced TODOs
  - [ ] audit written; no secrets/PII in logs
  - [ ] merged into `d2/work` and tagged `d2-<ID>-done`
- **Result:** commit `<sha>`, tag `d2-<ID>-done`, tests `<pass>/<total>`
- **Defaults used (Runbook §6):**
- **Follow-ups / out of scope noticed:**
- **Replace later (only for DONE_WITH_FAKES):**
- **Hand-off notes (who consumes this and how):**

---

## 5. Blocked

| Ticket | Since | What is blocked and why | What was tried | What is needed and from whom |
|---|---|---|---|---|

## 6. Decisions taken (defaults from Runbook §6, plus any new ones)

| # | Decision | Default or new | Ticket | Needs confirmation from |
|---|---|---|---|---|

## 7. Follow-ups (not in current tickets)

| Item | Found in | Suggested owner |
|---|---|---|

## 8. Open questions for the architect / product owner

| # | Question | Blocks | Default in use |
|---|---|---|---|

## 9. Session log (one line per session)

| Date | What was completed | Ended at |
|---|---|---|
