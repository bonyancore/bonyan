# Construction Connect — D2 Work Brief
### Zeyad (D2): Platform services → Cost control → Cash flow

**Audience:** the developer (D2) and any AI coding agent working in this repository.
**Source of truth:** the Notion library "Construction Connect". If this brief and a spec disagree, file `00` wins, then the module spec. Raise the conflict in your report; do not silently pick one.
**Built from:** specs `0` (START HERE), `00`, `01` (parts), `03`, `06` (parts), `08`, `10`, `20`, read on 29 Oct 2026.
**Tags:** `[spec]` = stated in the specs. `[rec]` = recommendation from the brief author, not in the specs. `[verify]` = must be checked before building. `[decide]` = needs a decision from the architect or product owner.

**Companion files (read in this order):**
0. **`Construction-Connect-D2-Agent-Runbook.md` FIRST**: how to run unattended, queue order, checkpoint-after-every-ticket rule, resume protocol, default decisions. Live state is in `D2-PROGRESS.md`.
1. This Brief: what to build, scope, dependencies, decisions needed.
2. `Construction-Connect-D2-Playbook.md`: how to work, security S1-S15, race catalogue, scalability, test playbook (endpoint matrix E1-E15, concurrency and failure-injection patterns).
3. `Construction-Connect-D2-Recipes-Platform.md` (PLT-201…211) and `Construction-Connect-D2-Recipes-Cost-Cash.md` (CST-201…206, CSH-201…202): step-by-step implementation, schema, endpoints, guards, tests per ticket.

**On process the Runbook wins; on technical rules the stricter file wins.**

If a recipe or the Playbook adds a rule not in this Brief, **follow the stricter one** and mention it in your report. Spec section numbers (`§`) in all files are from memory of the Notion pages: search the page by title if a number is off.

**Local copies:** the specs below are also exported in `docs/specs/` (read-only). Agents read the local copies; the links are for humans.

| Spec | Link |
|---|---|
| 0 START HERE (team plan) | https://app.notion.com/p/c424cdd2129682e7aec881633951e198 |
| 00 Architecture and conventions | https://app.notion.com/p/65d4cdd212968245ae4981ee33a8167c |
| 01 Platform foundation | https://app.notion.com/p/be34cdd2129682af9af901adfbca3462 |
| 08 Cost control | https://app.notion.com/p/49b4cdd21296830991c601bc700d8586 |
| 10 Cash flow | https://app.notion.com/p/63d4cdd2129683908b67012f0309884e |
| 20 Repository structure | https://app.notion.com/p/7a64cdd21296835086a2019d86fe9e76 |
| 06 Procurement (events you consume) | https://app.notion.com/p/a674cdd21296833199f081380c802500 |

---

## 0. Rules of engagement (read first, every session)

1. **One ticket at a time, finished and saved before the next.** Exactly one ticket `IN_PROGRESS`. Finish, test, **commit, update `D2-PROGRESS.md`, merge and tag**, then start the next (Runbook §2). The session can end at any time; unsaved work is lost.
2. **Stay in your lane.** You may edit:
   - `backend/src/Modules/Platform/**` (shared with D1: coordinate through PRs)
   - `backend/src/Modules/Cost/**`, `backend/src/Modules/Cash/**`
   - `backend/tests/Modules/Platform|Cost|Cash/**`
   - your own migrations, read models, jobs, docs.
3. **Never edit** another module's code, tables, migrations, or `*.Contracts`. `backend/src/Shared/**` and every `*.Contracts` project need the **architect's** review (CODEOWNERS): propose the change, do not merge it yourself.
4. **Need something from another module?** In this order:
   1. use its published `.Contracts` interface or event;
   2. use an in-memory fake from `Construction.Testing`;
   3. if neither exists, write a one-page `docs/rfc/NNNN-title.md`, define a port (interface) inside **your own** module's `Core/Application/Ports`, code against it, and stop there.
   Never "just fix it" over the fence.
5. **Do not invent requirements, and do not stall.** If the spec is silent or contradictory, apply the default in Runbook §6 (or, if none, the safest reversible option), record it in `D2-PROGRESS.md` as a decision, and continue.
6. **Best-case code on every ticket** (section 5 checklist).
7. **Tests after every ticket; deeper tests after risky ones** (section 6). A ticket is not done until its tests pass and the CI gates relevant to it pass locally.
8. **No scope creep.** If a spec section is marked R1.5, R2 or R3, or is not in your ticket, leave it out. Note it under "Out of scope / follow-ups".
9. **Report after every ticket** using the template in section 9.

---

## 1. The project in one page

**Construction Connect** is an Arabic-first operating system for contractors in Egypt (Saudi and the Gulf later). A contractor runs all projects, from tender to handover, on one shared data model, so every number traces back to the record that produced it.

**The money loop (our focus for R1 "lite"):**

```
site request → purchase request → PO → goods receipt → invoice match → cost ledger → cash schedule
                                                          ↘ payment application (مستخلص) → receivable
```

**Four cost states are kept apart and never summed** `[spec 00 BR-X-02]`:

| State | Meaning | Posted when |
|---|---|---|
| committed | promised (PO, subcontract) | PO issued |
| accrued | delivered or measured | goods receipt posted |
| invoiced | supplier invoice matched | invoice matched |
| paid | cash out | payment executed |

Only **accrued** is "actual cost" (AC) for earned value. No field named "total cost" may exist.

**Why your work matters:** Platform services are the plumbing every module depends on. Cost is the numerical truth of every project. Cash answers "in which week do we run out of money?". Mistakes here corrupt money numbers for every customer.

**Non-goals for v1:** microservices, event streaming platforms, a general workflow engine, real-time collaborative editing, full offline sync.

**Team:** 4 backend developers plus an architect. You are **D2** (pair "Platform and money", lead D1). Backups: D1 and D2 cover each other. Procurement is **D4 (Mohamed, per the assignment)**, who is waiting for `PLT-206` and whose `PRC-408/409/410` you depend on.

---

## 2. Architecture rules that apply to every ticket

**Stack `[spec 00]`:** .NET 10 modular monolith (one `Api` host, one `Worker` host), PostgreSQL (one schema per module, RLS on every tenant table), outbox events, object storage (S3 API; MinIO locally). Python intelligence service is separate and not yours.

### 2.1 Folder shape (per module, generated from the template)
```
Modules/Cost/
  Cost.Contracts/          PUBLIC: DTOs, query interfaces, events (+ JSON schema), Permissions.cs, ErrorCodes.cs, ModuleInfo.cs
  Cost.Core/               Domain (aggregates, rules) + Application (commands, handlers, validators, Ports) + Integration/{Inbound,Outbound,Acl}
  Cost.Infrastructure/     EF Core mappings, migrations, repositories, read models (rm_*), jobs, adapters
  Cost.Api/                Minimal API endpoints, request/response, registration
  CostModule.cs            implements IModule
```
Feature folders mirror each other: `PurchaseOrders/Issue` exists in both `Api` and `Core/Application`.

### 2.2 Dependency rules (enforced by architecture tests)
- `X.Core` has **no** EF Core, ASP.NET or Hangfire references.
- A module talks to another only through its `.Contracts`. Never read or write another module's tables.
- No circular dependencies. Only `Integration/Outbound` classes construct integration events.
- Every endpoint, job and consumer declares a `module_code` (`platform`, `cost`, `cash`). A build test fails if one is missing.
- Every public command handler has a validator.

### 2.3 Data conventions
- Ids: `uuid` v7, generated in the application. Every tenant table has `tenant_id uuid not null` (first column of composite indexes) **and RLS** using `current_setting('app.tenant_id')`.
- Standard columns: `id, tenant_id, created_at/by, updated_at/by, row_version (xmin), is_deleted, source`. **Ledger-style tables are append-only**: no `updated_*`, no deletes, corrections are reversing rows.
- Money: `decimal` in C#, `numeric(18,4)` in PostgreSQL, `Money` value object. **Never float.** Quantities `numeric(18,4)` + `unit_code`.
- Time: `IClock` only (never `DateTime.UtcNow`). `timestamptz` in UTC. Business dates in project timezone (`Africa/Cairo` default).
- Cross-module ids are by value with **no DB foreign key**. Exceptions allowed: `core.projects`, `core.work_packages`, `core.parties`, `platform.users`.
- Migrations: EF Core per module, forward-only, backward-compatible steps. Never edit another module's migration.
- Statuses: `status` text with a `CHECK` list; transitions through an explicit `Transitions` table in the aggregate; invalid transition = `409 invalid_state_transition`. Status changes go to `platform.status_history`.
- Approved or issued records are frozen: a change creates version `n+1` with `supersedes_id` and `change_reason`.

### 2.4 API conventions
- `/api/v1/{module}/{resource}`, plural nouns, kebab-case paths, camelCase JSON.
- `GET` read, `POST` create or **action** (`POST /budgets/{id}/submit`), `PATCH` (JSON Merge Patch) with `If-Match`. **No free `status` field in PATCH.**
- Keyset pagination (`?limit=50&cursor=`), `ETag` on reads, `412` on stale `If-Match`.
- `Idempotency-Key` on creating POSTs (mandatory for mobile/portals).
- Errors: RFC 9457 `application/problem+json`, `code = <module>.<snake_case>`, Arabic and English text in `messages.ar.json` / `messages.en.json` **in the same PR as the code**. HTTP mapping: 400 malformed, 401, 403, 404, 409 state conflict or duplicate key, 412 stale, 422 business rule, 429, 5xx never leaks internals.
- Hidden fields are **omitted**, not nulled (`IFieldVisibility`).

### 2.5 Events
Envelope `[spec 00 §3.9]`:
```json
{
  "eventId": "uuid-v7",
  "type": "cost.budget.approved.v1",
  "occurredAt": "2026-10-09T09:41:00Z",
  "tenantId": "uuid",
  "projectId": "uuid|null",
  "actorId": "uuid|null",
  "correlationId": "uuid",
  "causationId": "uuid|null",
  "payload": { }
}
```
- Name = `<module>.<aggregate>.<past_tense_verb>.v<N>`. Payloads are additive within a version.
- Written to `platform.outbox` **in the same transaction** as the change. Delivery is at-least-once, in order **per aggregate** (partition key = aggregate id).
- Consumers store `(consumer, event_id)` in `platform.processed_events` and ignore duplicates; handlers upsert by a natural key and ignore events older than the stored version.
- Retry: 5 attempts at 10 s, 1 m, 5 m, 30 m, 2 h, then `platform.outbox_dead`.
- Every event has a JSON schema in `contracts/events/*.schema.json` (repo root; read `contracts/README.md` for the naming rule) and a contract test for publisher **and** each consumer.
- Translate other modules' events in `Integration/Acl` into your own terms. Do not use another module's DTOs inside your domain.

---

## 3. Scope, order and dependencies

Order from START HERE: Sprint 0 and Wave 1 (platform), Wave 2 (cost), Wave 3 (cash). CI/CD runs throughout.

| Phase | Tickets | Depends on |
|---|---|---|
| Sprint 0 (weeks 1-2) | PLT-201, PLT-202, PLT-203, PLT-205, PLT-211 | Architect + D1 skeleton (Kernel, Api host, tenancy/RLS) |
| Wave 1 (~3 sprints) | PLT-204, **PLT-206 (ship early)**, PLT-207, PLT-208, PLT-209, PLT-210 | Sprint 0 outbox and worker |
| Wave 2 (~3 sprints) | CST-201 … CST-206 | PLT-206, PLT-208, PLT-210; D4's `PRC-408/409/410`; D3's planning events |
| Wave 3 (~3 sprints) | CSH-201, CSH-202 | Cost events; D1's contracts events (end of Wave 2) |

**Who is waiting for you**
- D4 (procurement): `PLT-206` number series; `CST-206` for its local budget-check table.
- Docs (D4): `PLT-207` files.
- Everyone: outbox, worker, idempotency, CI.

**Waiting on others → use fakes until real events exist**
- `CST-203` ← `PRC-408/409/410` (D4): PO issued, goods receipt posted, invoice matched.
- `CST-204` ← D3: `planning.progress.verified.v1`, `planning.schedule.baselined/revised.v1`.
- `CSH-201` ← D1 contracts events and D4 procurement events.

**Numbering note:** ticket ids in this assignment (`PLT-2xx`, `CST-2xx`, `CSH-2xx`) do not match the spec ticket ids (`CSH-001…`, `CST-E1…`). File `16` (ticket index) is not written. Keep a mapping table in `docs/modules/` `[decide]`.

---

## 4. Definition of Ready and Done `[spec 00 §9]`

**Ready:** problem and value stated; business rules numbered; fields and validations listed; API drafted; permissions identified; events identified; acceptance criteria in Given/When/Then; test data identified; dependencies linked; estimate agreed.

**Done:** code merged with review; unit + integration tests pass; tenant isolation tests extended; OpenAPI diff reviewed; events have JSON schemas and contract tests; migrations apply from empty **and** from the previous release; audit and permissions verified; error codes in the message catalogue (ar and en); logs and metrics added; docs updated; demo against the golden project when numbers are involved.

**CI gates (all must pass):** build, unit, integration (Testcontainers), architecture, contract (events + OpenAPI diff), tenant isolation, migrations from empty and from previous release.

---

## 5. Best-case code checklist (apply to every ticket)

**Design**
- [ ] Domain rules live in aggregates or pure functions in `Core`. No `DbContext`, `HttpContext`, or vendor SDK in `Core`.
- [ ] Rich aggregates for records with rules and money; plain CRUD for reference data.
- [ ] State machines are explicit enums + `Transitions`; invalid transition throws `DomainRuleViolation` with a stable error code.
- [ ] Plain `ICommandHandler<T>` resolved by DI. **No MediatR, no AutoMapper** `[verify licence]`. Mapping by hand or Mapperly.
- [ ] FluentValidation for request shape; domain invariants inside the aggregate.
- [ ] One class per file named for what it is: `Command`, `Handler`, `Validator`, `Endpoint`, `Request`, `Response`.
- [ ] No new NuGet package without architect approval (`Directory.Packages.props` is central).

**Correctness**
- [ ] Money as `decimal`/`Money`; no rounding of intermediates; round only at the documented step.
- [ ] Every consumer idempotent (processed_events + natural-key upsert). Every job idempotent with a tenant context and a timeout.
- [ ] Outbox write and business change in the **same** transaction.
- [ ] Fail closed: missing tenant context rejects the operation.
- [ ] Optimistic concurrency (`ETag` / `If-Match`) on mutating endpoints. Use advisory locks only where the spec says.
- [ ] Parameterised SQL only. No SQL or `DbSet` touching another module's schema.

**Operability**
- [ ] Serilog structured logs with correlation id; no secrets, no PII beyond names/phones/emails in logs.
- [ ] Metrics where the spec names them (e.g. outbox lag, dead-letter count).
- [ ] Error codes + ar/en messages shipped with the code.
- [ ] Short branches, PRs under about 400 changed lines, feature flags for unfinished work.

---

## 6. Test policy

**Detailed method (harness, endpoint matrix E1-E15, concurrency and failure-injection patterns, security tests) is in `Construction-Connect-D2-Playbook.md` §5. The summary below is the minimum.**

**After every ticket, run and pass:** unit/domain tests of the module, integration tests of the module, architecture tests, contract tests for any event touched, tenant isolation for every new table or endpoint.

**Risk tiers** (add the extra tests *before* reporting the ticket done):

| Tier | Tickets | Extra tests required |
|---|---|---|
| **High** | PLT-202, PLT-205, PLT-206, PLT-208, CST-201, CST-202, CST-203, CST-204, CST-205, CSH-201 | Concurrency and failure-injection tests; property-based tests (FsCheck) for any money or number logic; golden-project assertion where numbers are involved; negative/permission tests; tenant isolation incl. raw SQL against RLS |
| **Medium** | PLT-204, PLT-207, PLT-209, PLT-210, PLT-211, CST-206, CSH-202 | Integration tests incl. one failure path per endpoint or consumer; contract tests; limits and boundary tests |
| **Low** | PLT-201, PLT-203 | Unit tests + one integration test; architecture test for the shape |

**Always-on test types:** xUnit + Shouldly (unit), FsCheck (property-based), Testcontainers PostgreSQL + MinIO with `WebApplicationFactory` (integration), JSON-schema contract tests, NetArchTest (architecture), dedicated tenant-isolation suite, golden project scenarios.

**Rules for tests**
- Tests prove behaviour from the spec, not the implementation. Name them after the rule (e.g. `Ledger_rejects_update_via_trigger`).
- No `Thread.Sleep`; poll with a timeout. Tests must be deterministic (use `IClock` fakes).
- Failing golden numbers fail the build. Never "adjust" a golden number to make a test pass.

---

## 7. Ticket briefs

Each brief: **Goal · Spec · Must do · Constraints · Depends / Blocks · Risks · Done when · Tests.** "Done when" marked `[spec]` is quoted from the assignment; the others are **derived** from the specs and should be confirmed.

---

### Platform services

#### PLT-201 — Messaging library [M] · Risk: Low
- **Goal:** the shared building blocks for events: envelope, `IOutbox`, handler interfaces, idempotent consumer base.
- **Spec:** 00 §3.9, 20 §2 (`Construction.Messaging`).
- **Must do:** event envelope type (fields in section 2.5); `IOutbox.Publish(event)`; `IEventHandler<T>` and an idempotent consumer base that records `(consumer, event_id)` and skips duplicates; schema helpers for contract tests.
- **Constraints:** `Messaging` references **only `Kernel`**. `.Contracts` may reference `Messaging` for event base types only. No business rules. `backend/src/Shared/**` is **architect-owned**: open a PR and get review `[spec CODEOWNERS]`.
- **Blocks:** PLT-202, PLT-204, every consumer in the system.
- **Done when (derived):** a sample event type and handler compile against the library; architecture test confirms `Messaging` has no module references.
- **Tests:** unit tests for the consumer base (duplicate skipped, handler exception not recorded as processed); architecture test.

#### PLT-202 — Outbox [L] · Risk: **High**
- **Goal:** reliable event publication: write in the same transaction as the change; a dispatcher in the Worker delivers using `FOR UPDATE SKIP LOCKED`.
- **Spec:** 00 §3.9, 01 §4.5.
- **Must do:** `platform.outbox` (append-only); outbox writer in `Construction.Persistence`; Worker dispatcher; dispatch marker; lag metric.
- **Constraints:** delivery at-least-once; ordering **per aggregate** (partition key = aggregate id). Targets: outbox lag p95 under 5 s, alert above 60 s `[spec 00 §3.17, §3.16]`.
- **Risks:**
  - `[rec]` `SKIP LOCKED` with several dispatchers can break per-aggregate order. Either claim rows per aggregate, or run one dispatcher in R1. Record the choice in an ADR `[decide]`.
  - A crash after delivery but before marking causes redelivery; consumers must be idempotent.
- **Done when `[spec]`:** a test event reaches a test consumer **exactly once** (effect-once through the idempotent consumer).
- **Tests (High):**
  1. rollback of the business transaction leaves no outbox row;
  2. two dispatchers running concurrently never deliver the same row twice;
  3. crash-between-deliver-and-mark scenario results in one processed effect;
  4. events of one aggregate are consumed in order;
  5. lag metric emitted.

#### PLT-203 — Worker host and job runner [M] · Risk: Low
- **Goal:** the second entry point (`Construction.Worker`) and a reliable job runner.
- **Spec:** 00 §3.10, 20 §2/§6.
- **Must do:** queues `critical`, `default`, `reports`, `collectors`, `ai`; `platform.job_runs`; every job has a tenant context, a timeout, an enable flag per tenant, and declares `module_code`; cron in tenant timezone.
- **Constraints:** Hangfire with PostgreSQL storage `[verify package maintenance]` or a hand-written worker; decide in an ADR `[decide]`. `AppModules.cs` controls which modules register jobs and consumers.
- **Done when (derived):** a sample job runs on schedule, writes `job_runs`, times out as configured, and runs per tenant.
- **Tests:** unit + one integration test (job_runs row, timeout, missing tenant context fails closed).

#### PLT-204 — Retries, dead-letter, replay [M] · Risk: Medium
- **Goal:** failed handlers retry with backoff; exhausted events are parked and replayable.
- **Spec:** 00 §3.9 rule 3.
- **Must do:** 5 attempts at 10 s, 1 m, 5 m, 30 m, 2 h; then `platform.outbox_dead`; replay endpoint (admin); dead-letter count metric and alert.
- **Constraints:** replay must still be idempotent. Permission code for replay follows `module.resource.action` `[verify in 01 §10]`.
- **Done when (derived):** a handler that always fails ends in the dead-letter table after the 5th attempt; replay after the fix processes it once.
- **Tests:** retry schedule (fake clock), dead-letter row content, replay success and replay-twice safety.

#### PLT-205 — Idempotency-Key middleware [M] · Risk: **High**
- **Goal:** safe retries for mobile and portals.
- **Spec:** 00 §3.6.
- **Must do:** store `(tenant, key, request hash, response)` for **24 h**. Same key + same body: replay the stored response. Same key + different body: `422 idempotency_key_reuse`.
- **Constraints:** mandatory for mobile/portal creating POSTs, recommended for web. Key is a UUID. Middleware lives in `Construction.Web` (architect-owned) `[spec 20]`.
- **Risks:** `[rec]` two simultaneous requests with the same key: guard with a unique insert or lock so the second waits or gets a defined conflict, never a double execution `[decide]`.
- **Done when (derived):** the three behaviours above hold under concurrency.
- **Tests (High):** replay, reuse with different body, concurrent same-key requests (one execution), expiry after 24 h (fake clock), tenant isolation of keys.

#### PLT-206 — Number series [M] · Risk: **High** · **Ship early (D4 needs it)**
- **Goal:** human-readable business numbers (`PO-1066`, `PR-0412`, `VO-05`, `PA-04`, `BUD-0001`, `BT-012`, `MA-0007`).
- **Spec:** 00 §3.1, §6 (`INumberSeries.Next(tenant, series, project?)`).
- **Must do:** gapless per tenant and series **inside a successful transaction**; never reused; cancelled records keep their number; prefix and padding configurable per tenant; unique `(tenant_id, number)`.
- **Constraints:** `[rec]` the practical approach is a lock on the series counter row inside the caller's transaction, so a rollback returns the number. Confirm with the architect `[decide]`. Whether any series restarts yearly is an open business question.
- **Coordination:** START HERE puts a Sprint 0 skeleton with D4 and the hardening with you. Agree who owns which part before starting.
- **Done when (derived):** N parallel transactions produce no duplicates and no gaps; a rolled-back transaction does not consume a number.
- **Tests (High):** concurrency test (for example 50 parallel inserts), rollback test, per-tenant independence, per-series independence, prefix/padding config.

#### PLT-207 — Files [L] · Risk: Medium · Docs depends on this
- **Goal:** upload and download of photos, delivery notes, invoices, drawings.
- **Spec:** 00 §3.11, 01 §4.7 and §5.4 (file state machine `[verify: not read]`).
- **Must do:**
  - metadata in `platform.files` (`id, tenant_id, storage_key, original_name, content_type, size, sha256, scan_status, uploaded_by, linked_entity_type, linked_entity_id`);
  - upload: `POST /files/uploads` returns a **pre-signed PUT**; client uploads directly; then `POST /files/{id}/complete`;
  - limits: 25 MB default, 100 MB for drawings; allowed-type whitelist; antivirus scan job; thumbnails by the Worker;
  - download: short-lived pre-signed GET **after a permission check**; Arabic file names preserved (UTF-8 name stored separately from the storage key).
- **Constraints:** storage behind a port (`IFileService`); the S3-compatible adapter lives in `backend/src/Integrations/Integrations.Storage` (architect-owned folder) `[spec 20]`.
- **Done when (derived):** upload, complete, download works against MinIO in tests; limits and type whitelist enforced; cross-tenant download impossible.
- **Tests:** size and type limits, `complete` without an uploaded object, scan status gating, Arabic file name round trip, tenant isolation, download permission denied.

#### PLT-208 — Approvals engine [L] · Risk: **High**
- **Goal:** one-step approval with routing by amount; a user cannot approve their own request.
- **Spec:** 00 §6 (`IApprovalService.Request/Decide`), BR-X-07; 0 START HERE section 5 (R1 lite: one-step with amount-limit routing); 01 §4.4.
- **Must do:** approval instances routed by `approval_policy` (record type × amount × project × trade); segregation of duties; one pending instance per record; events `platform.approval.requested/approved/rejected/cancelled.v1` published through the outbox; owning modules react by `recordType` (e.g. `cost.budget`).
- **Out of scope in R1 lite:** multi-step chains, delegation, escalation, global search (R1.5 or later).
- **Conflict to resolve first `[decide]`:** spec 08 describes budget approval as two steps (project manager, then finance controller). R1 lite has **one step**. Ask the product owner whether budgets route by amount to one approver, or whether the two-step need pulls multi-step forward.
- **Done when (derived):** a request above a threshold routes to the right approver; the submitter cannot approve; approved/rejected events reach a test consumer.
- **Tests (High):** boundary amounts around each limit, self-approval blocked (and the tenant-enabled exception flagged), rejected path, double-decide rejected, event published in the same transaction, permission and tenant isolation.

#### PLT-209 — Notifications [L] · Risk: Medium
- **Goal:** producers publish **intents**; the platform renders and delivers.
- **Spec:** 00 §3.12.
- **Must do:** `INotificationService.Send(intent)` with `template_code`, `recipients`, `params`, `link`; versioned `ar` and `en` templates; channels **in-app and email**; user preferences and quiet hours; approvals and security notices bypass quiet hours.
- **Constraints:** never raw text from producers. WhatsApp-style channel is out of R1 `[spec 0]`. Server-generated text resolves on the server with `ar` default.
- **Done when (derived):** an intent produces an in-app and an email delivery in the user's language, honouring preferences.
- **Tests:** template resolution and fallback, preference respected, quiet hours with and without bypass, missing template error code.

#### PLT-210 — Excel import framework [L] · Risk: Medium
- **Goal:** three-step import used by BOQ, budget and master data.
- **Spec:** 00 §3.14, 01 §4.10.
- **Must do:** `IImportService` with `Parse`, `Preview`, `Commit`; preview shows parsed rows and **errors per row and column in Arabic**; commit is transactional (all-or-nothing or "skip errors" per import type); `platform.import_batches` (source file, counts, verification status, user); imported records carry `source='import'` and `import_batch_id`.
- **Targets:** a 5,000-row BOQ preview in under 10 s `[spec 00 §3.17]`.
- **Constraints:** modules plug their own handlers in (e.g. `ImportBudgetHandler` in Cost); you build the framework only.
- **Done when (derived):** a sample import type goes through upload, preview with row errors, commit.
- **Tests:** error rows reported with row and column, atomic commit, skip-errors mode, large-file timing, duplicate handling.

#### PLT-211 — CI/CD [L] · Risk: Medium
- **Goal:** the pipeline that enforces everything in this brief.
- **Spec:** 0 section 7, 20 §2 and §12.
- **Must do:** build; unit and integration tests (Testcontainers); architecture tests; contract tests (event schemas, OpenAPI diff); tenant isolation suite; migrations from empty and from the previous release; deploy to a dev environment. Managed hosting and managed PostgreSQL; secrets never in the repository.
- **Constraints:** `.github/`, `deploy/`, `backend/tools/` are owned by D2 + architect. Architecture rules themselves are the architect's.
- **Done when (derived):** a failing architecture rule, a broken event schema, or a breaking OpenAPI change each fail the build.
- **Tests:** deliberately break each gate once in a throwaway branch and confirm it fails.

---

### Cost control

**Shared facts for all CST tickets**
- Schema `cost`, module code `cost`, ticket key `CST`. Error codes `cost.*` (examples: `cost.budget_not_found`, `cost.draft_exists`, `cost.budget_not_editable`, `cost.budget_incomplete`, `cost.line_invalid`, `cost.period_closed`, `cost.period_not_ready`, `cost.period_sod`, `cost.reason_required`, `cost.entry_already_reversed`).
- Cost never reads procurement tables: it keeps its own replicas (`ref_work_packages`, `ref_cost_codes`, `wp_progress_points`, `wp_planned_curve`) fed by events, plus `processed_events`.

#### CST-201 — Budget [L] · Risk: **High**
- **Goal:** versioned, approved budgets per work package and cost code.
- **Spec:** 08 §4.3, §5.1, BR-CST-01…05, §7.1.
- **Must do:**
  - tables `cost.budgets` (aggregate root `CostBudget`) and `cost.budget_lines`;
  - lifecycle `draft → submitted → approved | rejected | cancelled`, older approved version becomes `superseded`;
  - exactly **one current approved version per project** (partial unique index on `is_current`);
  - **only a draft is editable**; one draft per project (`cost.draft_exists`);
  - version `n+1` with `change_reason` for any change (BR-X-03); versions never deleted;
  - line invariants (BR-CST-03): five category amounts ≥ 0, `total = sum`, `budget.total = sum(lines)`, `budget_qty × budget_unit_rate ≤ total`, unique `(WP, cost code)` per version, cost code must be a leaf and active, work package active and in the project;
  - approval through PLT-208; submitter cannot approve; import through PLT-210.
- **Constraints:** categories are `material, labour, subcontract, equipment, other`; the ledger also allows `unclassified`. Currency is base currency (EGP) in R1.
- **Out of scope:** budget transfers and variation revision proposals (not in your ticket list) `[decide]`.
- **Depends:** PLT-208, PLT-206 (`BUD-0001`), PLT-210, `core` work packages and cost codes.
- **Done when (derived):** a budget is created, submitted, approved; a second version supersedes the first; an invalid line is rejected; `GET /projects/{id}/budget/current` returns the single current version.
- **Tests (High):** every invariant above as its own test; FsCheck: `sum(lines) == header total` for random lines; concurrent approvals leave exactly one `is_current`; edit of non-draft rejected; tenant isolation; permission matrix; the golden plumbing budget (BAC 1,200,000).

#### CST-202 — Cost ledger [L] · Risk: **High**
- **Goal:** the append-only ledger holding the four states.
- **Spec:** 08 §4.3 (`cost.cost_entries`), BR-CST-11…18, 00 §4.3.
- **Must do:**
  - `cost.cost_entries` with `measure` (`committed | accrued | invoiced | paid`), `entry_kind`, **signed** `amount`, `currency`, `fx_rate`, `amount_base` (all reporting uses base), `vat_amount_base` (informational only), `posting_date`, `period_id`, source columns, `posting_key`, `reverses_entry_id`, `group_id`, `reason`;
  - **no `updated_*`, no `is_deleted`**; a DB **trigger rejects UPDATE and DELETE**, and the application role has no UPDATE grant;
  - unique `(tenant_id, posting_key)` for idempotency; unique partial `reverses_entry_id` (one reversal per entry);
  - corrections are `reversal` entries with the opposite sign; unallocated cost is posted with null work package and cost code and shown separately;
  - **VAT never enters a measure**; no field named total cost or spend.
- **Depends:** CST-205 for the period guard.
- **Done when (derived):** entries can be inserted but not updated or deleted (even with raw SQL); a duplicate `posting_key` is ignored; a second reversal of the same entry fails with `cost.entry_already_reversed`.
- **Tests (High):** trigger rejects UPDATE/DELETE; reversal rules; `posting_key` replay storm (same events twice, random order, identical ledger); amount sign equals `amount_base` sign; tenant isolation incl. raw SQL; FsCheck on `amount_base = round(amount × fx_rate, 4)`.

#### CST-203 — Cost consumers [L] · Risk: **High**
- **Goal:** turn procurement events into ledger entries.
- **Spec:** 08 BR-CST-11/12/13, §8.2; 20 §7 (integration map); 00 §4.3.
- **Must do (minimum in the ticket):**
  | Event | Ledger result |
  |---|---|
  | `procurement.purchase_order.issued.v1` | `commitment` +committed per line |
  | `procurement.goods_receipt.posted.v1` | `accrual` +accrued and `commitment_consumed` −committed, same value, shared `group_id` |
  | `procurement.invoice.matched.v1` | `invoice` +invoiced **plus** `accrual_trueup` so accrued equals the invoice value |
- **Gap to confirm `[decide]`:** the ticket lists three events, but the golden numbers also need **`payment.executed` → paid**, and correct behaviour needs **PO changed/cancelled/closed** (commitment change/release) and **receipt/invoice/payment reversals**. Ask the architect whether these belong to CST-203 or a follow-up ticket. Do not ship a ledger that cannot reach `paid = 300,000`.
- **Constraints:**
  - handlers live in `Cost.Core/Integration/Inbound/Procurement/`; translate into Cost's own `CommitmentEntry` in `Integration/Acl` (do **not** use procurement types in the domain);
  - `posting_key = <source_type>:<source_id>:<source_line_id>:<source_version>:<entry_kind>`, plus `:<event_id>` for reversals;
  - advisory lock on `(project, source_id)` so versions of one PO post in order;
  - closed-period guard (CST-205), late-posting policy `roll_forward` by default;
  - amounts ex VAT; VAT stored separately.
- **Depends:** D4's `PRC-408/409/410`; CST-202; PLT-201/202.
- **Blocked until real events exist:** build and test against event JSON schemas and a fake publisher; add the contract test for the consumer side.
- **Done when (derived):** the cement scenario posts committed +179,340; a goods receipt consumes the commitment and accrues the same value; an invoice of 181,200 against a receipt of 180,000 gives invoiced +181,200, true-up +1,200, accrued 181,200, VAT 25,368 only in `vat_amount_base`; replaying any event changes nothing.
- **Tests (High):** the scenarios above; duplicate and out-of-order delivery; unallocated line (no work package); closed-period receipt under `roll_forward`; reversal restores exact totals; contract tests against the three event schemas; "replay-storm" (10,000 events twice in random order gives an identical ledger).

#### CST-204 — Earned value [M] · Risk: **High**
- **Goal:** CPI, SPI, EAC from the ledger and **verified** progress.
- **Spec:** 08 BR-CST-31…42.
- **Must do:** `EarnedValueCalculator` as a **pure function** (no I/O):
  ```
  PV  = BAC × planned%        (current baseline curve, linear interpolation)
  EV  = BAC × actual%         (latest VERIFIED progress point ≤ status date; unverified never used)
  AC  = accrued               (cumulative, base currency)
  CV  = EV − AC     SV = EV − PV
  CPI = EV / AC     SPI = EV / PV      (null when AC = 0 / PV = 0; stored unrounded 8 dp, shown 4 dp)
  EAC = BAC / CPI   when actual% ≥ 5% and CPI > 0  (method "cpi")
  EAC = AC + (BAC − EV)  otherwise                   (method "budget_rate")
  ETC = EAC − AC    VAC = BAC − EAC    TCPI = (BAC − EV) / (BAC − AC)
  ```
  Parent work packages and project totals are **sums of leaves**; parent CPI/SPI come from summed EV/AC/PV, **never averaged**. Store `formula_version = 'ev-1.0'` and the exact inputs with the result (BR-X-10). Flag `progress_stale` when the latest verified point is older than the configured days.
- **Constraints:** needs replicas `wp_progress_points` and `wp_planned_curve`, fed by D3's planning events. Use fakes until they exist. PM override and forecast sign-off are not in your ticket `[decide]`.
- **Done when `[spec]`:** matches the golden project numbers (section 8).
- **Tests (High):** golden plumbing exactly; parent roll-up (EV 100/AC 125/PV 100 plus EV 300/AC 250/PV 200 gives CPI 1.0667, SPI 1.3333, not the average); null handling (AC 0 gives null CPI, no exception); EAC method switch at 5%; unverified progress ignored; FsCheck for invariants (`ETC = EAC − AC`, `VAC = BAC − EAC`); no rounding of intermediates.

#### CST-205 — Period close [M] · Risk: **High**
- **Goal:** closed periods reject postings unless reopened with a reason.
- **Spec:** 08 §5.4, BR-CST-19…26, BR-X-05.
- **Must do:**
  - `cost.cost_periods` (calendar month in project timezone), status machine `open → accruals_reviewed → forecast_updated → closed → reopened`;
  - synchronous guard `ICostPeriodGuard.IsOpen(project, date)` (procurement will call it);
  - posting into a closed period follows `late_posting_policy`: `roll_forward` (default: post in the earliest open period with `original_posting_date` and `is_late_posting = true`, raise `late_posting`) or `suspend` (park in `cost.posting_suspense`);
  - **reopen** needs permission `cost.period.reopen` and a reason, only for the latest closed period unless later ones are reopened first (reverse order); re-close increments `snapshot_version` and keeps the old snapshot;
  - **segregation of duties:** whoever prepared the period cannot close it (`cost.period_sod`).
- **Scope note `[decide]`:** the ticket text covers the guard plus close/reopen with reason. Accrual review, forecast sign-off and the full close pack are in spec 08 but not in this ticket; do not build them unless told.
- **Done when (derived):** a receipt dated in a closed month is rolled forward (or parked) with the alert; reopen restores normal posting; re-close creates snapshot version 2 and leaves version 1 intact.
- **Tests (High):** `late-receipt-closed-month` (both policies); reopen order rule; reopen without reason rejected; SoD; replayed events cannot bypass the guard; close-reopen-reclose snapshots.

#### CST-206 — Event `cost.budget.approved.v1` [S] · Risk: Medium
- **Goal:** let Procurement keep a local budget-check table.
- **Spec:** 08 §8.1.
- **Must do:** publish `cost.budget.approved.v1` when **version 1** is approved:
  ```
  payload: budgetId, projectId, version (1), totalAmount, currency,
           lines[{ workPackageId, costCodeId, totalAmount, categoryAmounts, budgetQty, unitCode }]
  ```
  It must be written to the outbox **in the same transaction as the approval**. Add the JSON schema under `contracts/events/` (repo root, follow `contracts/README.md`), the C# record under `Cost.Contracts/Events/`, and the contract test in `backend/tests/Contract.Tests`.
- **Gap to confirm `[decide]`:** versions after the first are published as `cost.budget.revised.v1` (adds `previousVersion`, `kind`, `changeReason`). Without it, Procurement's table goes stale after any revision. Ask whether to add it here.
- **Out of scope:** the Procurement-side consumer is D4's.
- **Done when (derived):** approving version 1 emits exactly one event; the schema validates; a rolled-back approval emits nothing.
- **Tests:** schema contract test, same-transaction test, no event on rejection, payload matches the golden budget.

---

### Cash flow

**Shared facts:** the schema is **`cash`** (00 wins over the "cashflow" wording in the assignment), module code `cash`.

#### CSH-201 — Receivable and payable schedules fed by events [L] · Risk: **High**
- **Goal:** schedules of money expected in and out, kept current by events.
- **Spec:** 10 §8.2 (handlers), BR-CSH-08/27/49; 00 §7 (events consumed by `cash`).
- **Must do:**
  - consumers record `(consumer, event_id)`, **upsert by a natural key** in one transaction with the row change, set the `is_dirty` flag, and ignore events older than the stored version (BR-CSH-27); after a successful apply, mark affected live forecasts dirty;
  - receivables: `contracts.contract.signed.v1` stores contract terms (no schedule yet); `contracts.payment_application.submitted.v1` creates an **expected** receivable with `gross = payableAmount × (1 − haircut)`, `due_date = expectedCertificationDate + payment_terms_days`, confidence **medium**; `contracts.payment_application.certified.v1` moves it to **certified** with the certified amounts, confidence **high**; `contracts.invoice_to_client.issued.v1` links to the certificate receivable;
  - payables: from procurement events (`invoice.matched`, `payment.scheduled/executed`) per spec 10 `[verify the full list in 10 §8.2]`;
  - failed events follow the 5-step retry and dead-letter (PLT-204).
- **Constraints:** do not read contracts or procurement tables; keep `contract_refs` and your own state. Retention, cheques, scenarios (R3) and payroll plans are not in this ticket.
- **Depends:** D1's contracts events (end of Wave 2), D4's procurement events, PLT-201/202/204. Use fakes and event JSON fixtures until then.
- **Done when (derived):** application PA-04 (section 8) produces the expected receivable at submission and the certified receivable at certification; replays and out-of-order delivery leave one correct row.
- **Tests (High):** golden PA-04 amounts; duplicate and out-of-order events; a late `submitted` must not downgrade an existing certified row `[verify the exact BR in spec 10]`; consumer contract tests against event schemas; tenant isolation; transactionality (failure rolls back the row and the dirty flag together).

#### CSH-202 — 13-week cash forecast view [L] · Risk: Medium
- **Goal:** for the finance controller: week by week for 13 weeks, money in, money out, balance, and the first week with a problem.
- **Spec:** 10 §1.1, §4.6 and §4.7, §5.4; events `cash.forecast.refreshed.v1` and `cash.shortfall.detected.v1` (00 §7).
- **Must do:** build the forecast from the schedules (CSH-201); read model (`rm_*`) with `as_of` freshness on every row; recompute when a forecast is marked dirty; publish `cash.shortfall.detected.v1` when the balance breaches the floor.
- **Open questions that change the design `[decide]`:** which balance is "opening" (a figure allocated to the project, or one company bank pool); the minimum-balance policy (one company number, per project, or weeks of payroll); whether uncertified applications appear in the base case and at what haircut; first day of the week.
- **Constraints:** heavy views read read models only, never scan transactional tables (p95 under 2 s for dashboards `[spec 00 §3.17]`).
- **Done when (derived):** a seeded set of schedules produces the expected weekly totals, balance line and first-shortfall week; the shortfall event fires once.
- **Tests:** weekly bucketing at week boundaries, opening-balance handling, shortfall detection and de-duplication, read model freshness, incremental result equals full rebuild.

---

## 8. Golden test data (a failing number fails the build)

**Plumbing trade (cost, EV):** BAC 1,200,000 · planned 50% · actual 40% · AC (accrued) 540,000
→ PV 600,000 · EV 480,000 · CV −60,000 · SV −120,000 · **CPI 0.8889 · SPI 0.8000 · EAC 1,350,000** · ETC 810,000 · VAC −150,000 · TCPI 1.0909.

**Five spend states (same trade):** committed gross 900,000 · accrued 540,000 · invoiced 450,000 · paid 300,000 · open commitment 360,000 · accrued-not-paid 240,000 · exposure (committed_open + accrued) 900,000.

**Budget check (for reference, Procurement calls it):** remaining = 1,200,000 − 900,000 = 300,000.

**Invoice true-up:** receipt 180,000; invoice 181,200 ex VAT; VAT 14% = 25,368 → invoiced +181,200; true-up +1,200; accrued 181,200; VAT only in `vat_amount_base`.

**Cement PO:** 60 t, chosen offer effective value 179,340 → committed +179,340; remaining 300,000 − 179,340 shown before and after.

**Payment application PA-04 (cash):** gross 2,445,500 · advance recovery 244,550 · retention 122,275 · deductions 15,000 · net 2,063,675 · VAT 288,915 · **payable 2,352,590**.

**Parent roll-up check:** trade A (EV 100, AC 125, PV 100) + trade B (EV 300, AC 250, PV 200) → project CPI 400/375 = 1.0667, SPI 400/300 = 1.3333.

---

## 9. Report template (send after every ticket)

```
Ticket: <id> — <title>            Risk tier: <High|Medium|Low>
Status: <done | blocked | partial>

What I built (3-6 lines):
Files changed (by project/folder):
Spec rules covered: <BR-… / section refs>

Tests added and results:
  - unit:        <n> passed
  - integration: <n> passed
  - contract / architecture / tenant isolation: <pass/fail>
  - extra (risk tier): <concurrency / failure-injection / property / golden>

Decisions I made (marked [decide]) and why:
Assumptions / deviations from spec:
Things I did NOT touch on purpose (other modules, Shared, Contracts):
Fakes or stubs I used and what real dependency replaces them:
Risks and follow-ups (out-of-scope items I noticed):
Questions for architect / product owner:
```

---

## 10. Decisions needed (collect and send once)

**The agent does not wait for these:** each has a default in Runbook §6 that it applies and records. Send this table to the architect/product owner so the defaults can be confirmed or changed.

| # | Question | Blocks |
|---|---|---|
| 1 | Budget approval: one step routed by amount (R1 lite) or two steps (spec 08)? | PLT-208, CST-201 |
| 2 | Outbox ordering per aggregate with multiple dispatchers: per-aggregate claim, or a single dispatcher in R1? | PLT-202 |
| 3 | Hangfire (PostgreSQL storage) or a hand-written worker? | PLT-203 |
| 4 | Same-key concurrent requests: wait or conflict? | PLT-205 |
| 5 | Who owns what of number series: D4's Sprint 0 skeleton vs your hardening? Any series that restart yearly? | PLT-206 |
| 6 | Does CST-203 include `payment.executed`, PO changed/cancelled/closed and reversals? | CST-203, the golden `paid` figure |
| 7 | Add `cost.budget.revised.v1` to CST-206? | Procurement's budget-check table |
| 8 | Scope of CST-205: guard + close/reopen only, or the full close workflow? | CST-205 |
| 9 | Cash: opening balance, floor policy, haircut on uncertified applications, first day of the week. | CSH-202 |
| 10 | Mapping between these ticket ids and the spec ids (file 16 does not exist). | Traceability |
