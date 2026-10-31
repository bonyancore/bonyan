# Construction Connect — D2 Engineering Playbook
### How to implement safely: protocol, security, race conditions, scalability, testing

Companion to `Construction-Connect-D2-Brief.md` (the *what*). This file is the *how*.
Per-ticket recipes: `Construction-Connect-D2-Recipes-Platform.md` and `Construction-Connect-D2-Recipes-Cost-Cash.md`.

Tags: `[spec]` stated in the specs · `[rec]` engineering recommendation, not in the specs · `[verify]` check the repo or spec before relying on it · `[decide]` needs the architect or product owner.
Spec section numbers in these files are from memory of the Notion pages; **search the page by title** if a number is off.
Route names, permission codes and table names marked `[rec]` must be aligned with the real `.Contracts` project and spec 08 §7 / 01 §7 before merging.

---

## 1. Protocol for an agent that must not improvise

### 1.1 The loop (per ticket, no skipping)
The session-level rules (start-of-session resume, checkpoints, queue order, statuses, `D2-PROGRESS.md`) are in **`Construction-Connect-D2-Agent-Runbook.md`, which wins over this file on process.** Inside one ticket:
1. **Read** the ticket brief (Brief §7) and the recipe (Recipes file) for the ticket. Read the linked spec page section if a rule is unclear.
2. **Write a plan** in 8 lines max: files you will create, tables, endpoints, events, tests. If any line touches another module or `Shared/**`, follow rules 0.3-0.4 of the Brief (fakes, ports, RFC), not a direct edit.
3. **Create the failing tests first** for the rules you are about to implement (at least the happy path and the top 3 failure paths).
4. **Implement the smallest vertical slice** (Runbook §3): domain → handler → persistence → endpoint. After each slice: tests green → commit → update `D2-PROGRESS.md`.
5. **Run the checks** (section 1.3). Fix until green.
6. **Add risk-tier tests** (Brief §6): concurrency, failure injection, property, golden.
7. **Self-review against** the Security checklist (section 2) and the Race catalogue (section 3). Tick each item that applies in `D2-PROGRESS.md`.
8. **Finish**: Definition of Done (Runbook §7) → status `DONE` → merge and tag → only then the next ticket. Never write "done" if a check is red.

### 1.2 When something is unclear or missing: do NOT stop the run
- The spec contradicts itself or this brief: follow the **stricter** rule, record the conflict in `D2-PROGRESS.md` → Open questions, continue.
- You need a change in another module, `Shared/**`, a `*.Contracts` project, or `Directory.Packages.props`: **do not edit them**. Use a fake or a port in your own module, write `docs/rfc/NNNN-title.md`, mark the ticket `DONE_WITH_FAKES` or `BLOCKED` per Runbook §4, and move on.
- A decision from Brief §10 is open: apply the **default in Runbook §6**, record it, continue.
- A test needs a golden number that is not in Brief §8: derive it from the spec formulas, put the derivation in the test name/comment, and flag it in `D2-PROGRESS.md`; never loosen a tolerance to pass.
- Three failed fix attempts on the same problem: Blocked protocol (Runbook §4).

### 1.3 Checks to run before reporting
The real commands are discovered in the bootstrap and stored in `scripts/d2/d2.conf`; `finish-ticket.sh` runs them. The block below shows what each check is for `[verify the exact commands in the repo README/Makefile]`:
```
dotnet build -warnaserror
dotnet test --filter "Category!=Slow"          # unit + integration for touched modules
dotnet test backend/tests/Architecture.Tests
dotnet test backend/tests/Contract.Tests              # event schemas + OpenAPI diff
dotnet test backend/tests/Tenant.Isolation.Tests
# migrations: apply to an empty DB, and from the previous release tag
```

### 1.4 Never do (hard list)
- Never use `float`/`double` for money or quantity. Never `DateTime.Now/UtcNow`.
- Never write to, read from, or join another module's schema. Never add a cross-module DB foreign key (exceptions in Brief §2.3).
- Never `UPDATE`/`DELETE` ledger-style tables. Never "fix" a golden number.
- Never accept `tenant_id`, `created_by`, `status`, `is_current`, or any server-owned field from a request body.
- Never log request bodies, tokens, pre-signed URLs, file contents, or full exception details to clients.
- Never put business rules in controllers/endpoints or in SQL triggers (the only triggers allowed are the ones this brief names: ledger append-only guard, `updated_at` if the template uses it).
- Never catch-and-swallow exceptions in consumers or jobs (they must fail so retry and dead-letter work).
- Never add a NuGet package, a new schema, or a new background host without architect approval.

### 1.5 Commit and PR rules
- Branch per ticket: `feat/<ticket-id>-short-name`. PR under ~400 changed lines; split by vertical slice.
- Migrations: one migration per PR, forward-only, additive (add nullable → backfill → enforce in a later release).
- Every PR description lists: ticket id, rules covered, tests added, risks, `[decide]` items.

---

## 2. Security baseline (every ticket must satisfy what applies)

| # | Rule | How to do it | How to test |
|---|---|---|---|
| S1 | **Tenant isolation is enforced in the database** | Every tenant table: `tenant_id uuid not null`, `ENABLE ROW LEVEL SECURITY` **and** `FORCE ROW LEVEL SECURITY`. Policy: `USING (tenant_id = current_setting('app.tenant_id', true)::uuid) WITH CHECK (same)`. The application connects as a **non-owner, non-superuser role without BYPASSRLS**. Set the tenant per transaction: `SELECT set_config('app.tenant_id', @tenant, true)` (`true` = transaction-local) at the start of every transaction; with a connection pool never use session-level `SET`. `[rec]` | Tenant isolation suite: insert rows for tenants A and B, connect as the app role with tenant A, run raw `SELECT`, `UPDATE`, `DELETE` and an `INSERT` with tenant B's id: all must return 0 rows or fail. Also run with **no** tenant set: must return 0 rows. |
| S2 | **Fail closed** | A request, job, or consumer without a resolved tenant context throws before touching data. `current_setting(..., true)` returns null → policy matches nothing. | Test: call a handler with no tenant context → exception, no rows. |
| S3 | **Authentication then authorization, deny by default** | Every endpoint declares required permission(s) (`module.resource.action`) in its registration; an endpoint with no declaration fails an architecture test. `[rec]` | Matrix test: each endpoint without a token → 401; with a token lacking the permission → 403. |
| S4 | **Object-level authorization** (BOLA/IDOR) | A permission code is not enough: for every project-scoped record also check the caller has access to **that project** (and, for fields, `IFieldVisibility`). Resolve the record by `(tenant_id, id)`; if it is in another project the caller cannot access, return **404**, not 403 (no existence leak). `[verify API name in 01]` | Test: user with `cost.budget.read` on project P1 requests a budget of P2 → 404. |
| S5 | **No mass assignment** | Requests are explicit DTOs. Server-owned fields (`id`, `tenant_id`, `status`, `version`, `is_current`, `created_*`, `source`, `posting_key`) are never bound from input. PATCH allow-lists fields; `status` changes only through action endpoints. | Test: send `{"status":"approved","tenantId":"<other>"}` in create/patch → ignored or 400; value unchanged. |
| S6 | **Validate everything at the edge** | FluentValidation on every command: lengths, ranges, enum membership, decimal precision (`numeric(18,4)`), max collection sizes (e.g. budget lines ≤ 5,000), max body size on endpoints, UUID format. Reject unknown enum strings. | Boundary tests per field: min, max, max+1, null, empty, wrong type. |
| S7 | **SQL injection** | EF Core or Dapper with parameters only. No string-built SQL. Identifiers (schema/table) are constants. | Static check in review; test with `'; DROP TABLE--` in text fields persisted and returned verbatim. |
| S8 | **Errors do not leak** | RFC 9457 `problem+json` with a stable `code`; 5xx returns a generic message plus `traceId` only. Never return stack traces, SQL, file paths, or other tenants' ids. | Test: force an exception → body has no stack trace or SQL text. |
| S9 | **Secrets and PII** | Secrets only from configuration/secret store, never in repo, logs, or test fixtures. Logs: structured, include correlation id; do not log bodies, `Authorization`, pre-signed URLs, or file names of confidential documents. | Log-capture test: sensitive values never appear. Secret-scanning in CI. |
| S10 | **Audit** | Every create, state change, approval decision, reopen, replay, and download of a controlled file writes an audit record (who, when, what, before/after for state). Audit rows are append-only. | Test: the action writes exactly one audit record with the actor. |
| S11 | **Segregation of duties** | Submitter ≠ approver; period preparer ≠ closer; enforced in the domain, not the UI. Based on `user_id`, not on role. | Each SoD rule gets a negative test and a "tenant-enabled exception" test if the spec allows one. |
| S12 | **Rate limiting and abuse** | Per-user and per-tenant limits on expensive endpoints (uploads, import, replay). Return `429` with `Retry-After`. `[rec]` | Test: exceed limit → 429. |
| S13 | **Replay and idempotency are scoped** | Idempotency keys are scoped to `(tenant, user)` and bound to the method + path + body hash. A key from another user or tenant never replays. | Test: same key, other user → fresh execution (not a replay). |
| S14 | **Supply chain and pipeline** | `dotnet list package --vulnerable` in CI, secret scanning, branch protection, CI cannot deploy with failing gates, deploy credentials least-privilege, migrations run with a **separate owner role**, the runtime role has only DML grants. | A CI job fails on a known vulnerable package (try once on a throwaway branch). |
| S15 | **Uploads and imports are hostile input** | See PLT-207 and PLT-210 recipes: size caps, type allow-list, magic-byte check, scan-before-use, zip-bomb limits, no formula evaluation, attachment disposition, `nosniff`. | Specific tests in the recipes. |

---

## 3. Race conditions and concurrency catalogue

Rule of thumb `[rec]`: **let the database decide**. Prefer unique indexes, conditional `UPDATE ... WHERE state = 'expected'` (check rows affected), `INSERT ... ON CONFLICT`, and `SELECT ... FOR UPDATE/SHARE` over application locks. Use advisory locks only where the specs say.

| Hazard | Where | Technique | Test |
|---|---|---|---|
| Double delivery of an event | Outbox, consumers | At-least-once is normal. Consumer records `(consumer, event_id)` in `platform.processed_events` **in the same transaction** as its effect; unique key makes the second attempt a no-op. | Deliver the same event 2× and 10× concurrently → one effect. |
| Event processed out of order | Consumers | Carry `aggregate_version` (or source version). Upsert only if `excluded.version > stored.version`. Ignore older. | Deliver v2 then v1 → state = v2. |
| Two dispatchers claim the same row | Outbox | Claim with `FOR UPDATE SKIP LOCKED` in a short transaction; store a lease (`locked_until`, `locked_by`); deliver outside the claim transaction; mark dispatched in a second transaction. | Run N dispatchers in parallel; each event delivered once (by effect). |
| Per-aggregate order broken by parallel dispatch | Outbox | An event is eligible only if there is no earlier **undispatched or leased** event of the same aggregate (`NOT EXISTS` check). See recipe PLT-202. | Interleaved events of 3 aggregates → each aggregate observed in order. |
| Crash after side effect, before marking done | Outbox, jobs | Idempotent consumers + lease expiry so another worker retries. | Kill mid-flight (throw after handler, before mark) → redelivery, one effect. |
| Same request retried (client timeout) | POST endpoints | `Idempotency-Key` row inserted with `INSERT ... ON CONFLICT DO NOTHING` **before** executing; a concurrent duplicate sees `in_progress` → `409 idempotency_in_progress`. | 10 parallel identical requests → one execution, rest replay or 409. |
| Number gaps or duplicates | Number series | Counter row updated with `UPDATE ... RETURNING` inside the caller's transaction (row lock held to commit). Call `Next()` as late as possible in the transaction. Unique `(tenant_id, number)` on target tables as a second guard. | 50 parallel creates → unique and gapless; rollback → number reused. |
| Double approval / double decision | Approvals | Conditional update: `UPDATE approval_instances SET status=@new WHERE id=@id AND status='pending'`; zero rows → `409`. | Two approvers decide simultaneously → one wins. |
| Two current budget versions | Budgets | Partial unique index `(tenant_id, project_id) WHERE is_current`; one draft per project via partial unique `WHERE status='draft'`. Approval flips old→false then new→true inside one transaction. | Approve two versions concurrently → exactly one current. |
| Lost update on edit | PATCH endpoints | `row_version` (`xmin`) via `ETag`/`If-Match`; stale → `412`. | Two clients patch the same row → second gets 412. |
| Posting into a period while it closes | Cost ledger | The posting transaction does `SELECT ... FROM cost.cost_periods ... FOR SHARE` before inserting; `close` does `UPDATE ... ` (exclusive) so it waits for in-flight postings and later postings see `closed`. | Start posting and close concurrently → no entry lands in a closed period. |
| Two versions of one PO posted out of order | CST-203 | `pg_advisory_xact_lock(hashtextextended(project_id||source_id, 0))` per `(project, source)` plus version check. | Deliver PO v1 and v2 in parallel → ledger consistent. |
| Double reversal | Ledger | Unique partial index on `reverses_entry_id`; `ON CONFLICT DO NOTHING`, then map to `cost.entry_already_reversed`. | Two concurrent reversals → one succeeds. |
| Overlapping recomputation | Cash forecast, EV snapshots | Advisory lock per `(tenant, project)` for the job; compute from one snapshot (`REPEATABLE READ`); write results with a `run_id` and flip a pointer so readers never see partial data. | Two recompute jobs at once → one runs, one skips. |
| Duplicate alert | Shortfall event | Fire on **transition** (not-in-shortfall → in-shortfall, or first-shortfall-week changes); store last state. | Recompute twice → one event. |
| Double import commit | Import | Conditional transition `previewed → committing` with `UPDATE ... WHERE status='previewed'`; zero rows → `409`. | Commit the same batch twice in parallel → one commit. |
| Orphan files | Files | `pending` rows expire; a janitor job deletes objects and rows older than 24 h. | Create pending upload, advance clock → removed. |

**Concurrency test pattern (use it everywhere):** start N tasks behind a barrier so they begin together, run the operation, assert on the **database state**, not on the responses alone. Run each concurrency test several times in CI (use `[Theory]` with repeats) because races are probabilistic.

---

## 4. Scalability and performance rules

Targets from the specs `[spec 00 §3.17 — verify]`: dashboard p95 < 2 s, outbox lag p95 < 5 s (alert > 60 s), 5,000-row BOQ import preview < 10 s.

1. **Index for the query you ship.** Every `tenant_id`-scoped table: composite indexes start with `tenant_id`. List endpoints use **keyset pagination** (`WHERE (created_at, id) < (@c, @i) ORDER BY created_at DESC, id DESC LIMIT n`), never `OFFSET`. Default `limit=50`, max `200`.
2. **No N+1.** One query per page; load children with a single `WHERE parent_id = ANY(@ids)`.
3. **Keep transactions short.** No network calls (storage, email, HTTP) inside a database transaction. Number series `Next()` goes last.
4. **Outbox and queue tables grow forever** unless cleaned: index `(dispatched_at, next_attempt_at)` partial `WHERE dispatched_at IS NULL`; a nightly job deletes dispatched rows older than N days (config, default 14) in batches of 10,000 `[rec]`. Never `DELETE` the ledger; partition the ledger by month later if volume demands (not in R1).
5. **Batch where it is safe.** Dispatcher claims 50-200 rows per round. Import commit inserts in chunks of 500-1,000 rows with `COPY` or multi-row inserts. Budget line bulk save is set-based.
6. **Backpressure.** Cap in-flight work per worker; if outbox lag exceeds the alert threshold, the dispatcher keeps its batch size, it never spawns unbounded tasks.
7. **Hot rows:** the number-series counter and the per-project ledger posting lock serialize. That is intended; keep the critical section tiny and measure.
8. **Read models for heavy views.** Dashboards and the 13-week view read `rm_*` tables, never scan transactional tables. Each read model row has `as_of`.
9. **Streaming for big files.** Upload/import: stream, never load a whole workbook into memory when avoidable; cap rows and uncompressed size.
10. **Stateless hosts.** No in-memory state that matters between requests (the Api and Worker can run N instances). Use the database for locks and leases.
11. **Ledger aggregation** `[rec]`: R1 computes totals from the ledger with covering indexes `(tenant_id, project_id, work_package_id, cost_code_id, measure)` and `(tenant_id, project_id, period_id, measure)`. Add a rollup read model only if the p95 target is missed under the load test.
12. **Add a load smoke test** per high-tier ticket: 1,000 events or 200 parallel requests on a Testcontainers DB; assert no errors, no duplicates, and a generous time limit (the point is to catch O(n²) behaviour, not to benchmark).

---

## 5. Testing playbook

### 5.1 Layers
| Layer | Tool | What it proves | Speed |
|---|---|---|---|
| Domain unit | xUnit + Shouldly | Rules, state machines, calculators (no I/O) | ms |
| Property-based | FsCheck | Invariants hold for random inputs (money, sums, ordering) | s |
| Integration | `WebApplicationFactory` + Testcontainers (PostgreSQL, MinIO) | Handlers + DB + endpoints + RLS + triggers | s |
| Contract | JSON-schema validation, OpenAPI diff | Events and API do not break consumers | s |
| Architecture | NetArchTest | Dependency, module_code, validator rules | s |
| Tenant isolation | dedicated suite as the app DB role | RLS works and fails closed | s |
| Golden | scenario tests with fixed numbers | Money math is correct | s |
| Load smoke | integration + parallelism | No O(n²), no races at small scale | s-min |

Mark slow tests `[Trait("Category","Slow")]`; the fast set must stay under about 5 minutes.

### 5.2 Integration test harness (copy this shape) `[rec]`
```csharp
// tests/<Module>.IntegrationTests/Support/ApiFactory.cs
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:17").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); /* run migrations as owner role, create app role */ }
    public new async Task DisposeAsync() { await _pg.DisposeAsync(); }

    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.UseSetting("ConnectionStrings:App", _pg.GetConnectionString()); // app role, NOT the owner
        b.ConfigureTestServices(s => {
            s.AddSingleton<IClock>(new FakeClock(new DateTime(2026,10,29,9,0,0,DateTimeKind.Utc)));
            s.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }

    // Authenticated client for a tenant/user with explicit permissions and project scope
    public HttpClient As(Guid tenant, Guid user, string[] permissions, Guid[]? projects = null) { /* set test headers */ }
}
```
Rules: one container per test class (`IClassFixture`) and **reset data per test** (unique tenant id per test is the cheapest isolation). Run migrations once. Use the **app role** connection for the system under test and a separate owner connection only to seed or to assert.

### 5.3 Standard endpoint test matrix (every endpoint gets all that apply)
| # | Case | Expect |
|---|---|---|
| E1 | Happy path | 200/201 + body shape; `Location`/`ETag` where defined; DB row exists |
| E2 | No token | 401 |
| E3 | Token without permission | 403 (problem+json, stable `code`) |
| E4 | Resource in another **tenant** | 404 |
| E5 | Resource in a project the user cannot access | 404 |
| E6 | Validation failures (each rule) | 400/422 with the correct error `code` and Arabic + English messages |
| E7 | Invalid state transition | 409 `invalid_state_transition` |
| E8 | Stale `If-Match` | 412; correct `ETag` → success |
| E9 | Duplicate create with same `Idempotency-Key` | same response, one row |
| E10 | Same key, different body | 422 `idempotency_key_reuse` |
| E11 | Mass assignment attempt | server-owned fields unchanged |
| E12 | Pagination (list endpoints) | stable order, `cursor` continues, `limit` capped |
| E13 | Response contains no hidden fields | omitted, not null (`IFieldVisibility`) |
| E14 | 5xx path | generic body, no stack trace |
| E15 | Audit/event side effect | exactly one audit row; exactly one outbox event; none on failure |

Example (shape to copy):
```csharp
[Fact]
public async Task Approve_budget_by_the_submitter_is_rejected()
{
    var t = Guid.NewGuid(); var u = Guid.NewGuid();
    var client = _f.As(t, u, new[] { "cost.budget.create", "cost.budget.submit", "cost.budget.approve" }, new[] { ProjectId });
    var id = await Seed.SubmittedBudgetAsync(_f, t, u);              // submitted by u
    var res = await client.PostAsync($"/api/v1/cost/budgets/{id}/approve", null);
    res.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    (await res.ReadProblemAsync()).Code.ShouldBe("platform.approval_self_approval");   // [verify code]
    (await Db.OutboxCountAsync(t, "cost.budget.approved.v1")).ShouldBe(0);
}
```

### 5.4 Concurrency test pattern
```csharp
var barrier = new Barrier(N);
var results = await Task.WhenAll(Enumerable.Range(0, N).Select(_ => Task.Run(async () => {
    barrier.SignalAndWait();
    return await client.PostAsJsonAsync("/api/v1/...", body, ct);
})));
// Assert on the DATABASE: duplicates, gaps, exactly-one current, exactly-one reversal, one outbox row.
```
Repeat 5-20× in CI. A test that passes once is not evidence.

### 5.5 Failure-injection pattern
Register a decorator in the test host that throws at a named point (`AfterSideEffectBeforeMark`, `BeforeCommit`, `AfterCommitBeforeDispatch`). Run the operation, then run the retry path, then assert the final state equals the no-failure state. Required for outbox, consumers, import commit, and period close.

### 5.6 Property-based tests (FsCheck)
Use for: line total = sum of categories; budget total = sum of lines; `amount_base = round(amount × fx_rate, 4)`; `ETC = EAC − AC`; roll-up equals sum of leaves; ledger totals independent of event order; replaying an event set twice gives the same totals. Generators must stay in realistic ranges (positive money up to 10^9, 4 dp).

### 5.7 Architecture and contract tests
- Architecture: Core has no EF/ASP.NET/Hangfire; no module references another's `Core`/`Infrastructure`; every endpoint/job/consumer has `module_code`; every command handler has a validator; every endpoint declares permissions; only `Integration/Outbound` constructs integration events.
- Contract: every published event validates against its JSON schema; every consumer test feeds the **publisher's** sample payload; OpenAPI snapshot diff fails on breaking changes.

### 5.8 What "enough tests" means per risk tier
Follow Brief §6. As a floor: **High** = all matrix rows E1-E15 that apply + concurrency + failure injection + property + load smoke; **Medium** = E1-E8, E14-E15 + one failure path + contract; **Low** = unit + one integration + architecture.

### 5.9 Flaky tests
Never mark a test "ignore" to go green. If a test is flaky, it is exposing a race or a time dependency: fix the code or use `IClock`/polling with a timeout. Report it.

---

## 6. Common traps (read once)
1. **Using the owner DB role in tests** hides RLS bugs. Always test as the app role.
2. **Session-level `SET app.tenant_id` with pooling** leaks a tenant into the next request. Use transaction-local `set_config(..., true)`.
3. **Publishing events after commit** (instead of the outbox) loses events on a crash. Always the outbox, same transaction.
4. **Treating 'exactly once' as real.** It is at-least-once plus idempotent effects.
5. **`OFFSET` pagination**, `ToList()` of a whole table, and N+1 queries.
6. **Rounding intermediates** in EV/EAC. Round only for display.
7. **Averaging CPI/SPI of children.** Sum EV, AC, PV, then divide.
8. **Letting a status be PATCHed.** Status changes through actions only.
9. **Pre-signed upload URLs cannot enforce size.** Verify size and type at `complete` and delete if invalid.
10. **Serving user files inline** with a guessed content type → stored XSS. Use `Content-Disposition: attachment` + `X-Content-Type-Options: nosniff`.
11. **Swallowing an exception in a consumer.** It stops retry and dead-letter from working.
12. **Time zones:** week and month boundaries use the project timezone (`Africa/Cairo`), not UTC.
13. **Seeding golden data and then asserting with loose tolerance.** Assert exact decimals.
14. **Generated mappings returning `null` for hidden fields.** Omit them.
