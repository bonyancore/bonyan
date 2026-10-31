# Construction Connect — D2 Agent Runbook
### Read this FIRST. It tells you how to run unattended, never lose work, and finish every ticket properly.

Files: Brief (what) · Playbook (how, security, races, tests) · Recipes-Platform · Recipes-Cost-Cash · **this Runbook** · `D2-PROGRESS.md` (your live state).
Tags: `[spec]` `[rec]` `[verify]` `[decide]` as in the other files.

---

## 0. Prime directive

> **Finish one ticket completely, save it, record it, and only then start the next.**
> Your session can end at any moment (usage limit, timeout, crash). Work that is not committed **and** written in `D2-PROGRESS.md` does not exist.

Corollaries:
1. Never have more than **one** ticket in progress.
2. Never end a turn with uncommitted work or a stale `D2-PROGRESS.md`.
3. Never mark a ticket `DONE` with a red, skipped, or missing required test.
4. Never wait for an answer. If you are unsure, apply the default in section 6, record it, and continue.

---

## 1. Session start (every single session, even if you think you remember)

1. Read this Runbook, then `docs/team/D2-PROGRESS.md`.
2. `git status` and `git log --oneline -10` and `git tag --list 'd2-*-done'`.
3. If the working tree is dirty: look at the diff, run the tests of the touched area, then either finish the slice and commit it, or commit it as `wip(<id>): <what>` and note it in `D2-PROGRESS.md`. Never discard work you did not understand.
4. Re-run the checks of the **last DONE ticket** (smoke, not full): if red, fix it before anything else and log it.
5. Pick the first ticket in the queue (section 5) whose status is not `DONE`/`DONE_WITH_FAKES`/`SKIPPED` and whose dependencies are satisfied. If a ticket is `IN_PROGRESS`, resume it from its last checkpoint.
6. **First run only (bootstrap).** Do it **inside the first ticket's branch, never on the base branch**: run `bash scripts/d2/start-ticket.sh PLT-201` first, then:
   - **first read the repository's own guides, they describe the real structure and win over any `[rec]` assumption about structure or naming:** root `README.md`, `docs/rfc/backend-setup-agent-guide.md`, `docs/adr/ADR-001-module-entry-point.md`, `docs/sprint-0-checklist.md`, `backend/src/README.md`, `backend/tests/README.md`, `contracts/README.md`, the `README.md` of `backend/src/Modules/Platform`, `Cost`, `Cash`, and `deploy/docker/initdb/01-roles-schemas.sql` (database roles and schemas). If one of them conflicts with these team files, follow the stricter rule and record the conflict;
   - inspect the repo (README, Makefile/scripts, `Directory.Packages.props`, `backend/src/Shared`, `tests/`) and discover the real build/test commands and the existing skeleton (Kernel, Persistence, Web, Testing, Architecture.Tests, module template);
   - write them in `D2-PROGRESS.md` → "Commands" and "Skeleton found", **and fill the `*_CMD` values in `scripts/d2/d2.conf`** (`finish-ticket.sh` refuses to run while `TEST_CMD` is empty);
   - confirm `BASE_BRANCH` in `d2.conf` is the repo's real default branch (if `start-ticket.sh` said it does not exist, stop and tell the human: do not guess);
   - adjust `ALLOWED_REGEX`/`REVIEW_REGEX` only if the real folder layout differs; keep the rules strict; list the change in the report;
   - confirm `docs/specs/` exists and list which spec files are present in `D2-PROGRESS.md` (missing ones go to Open questions);
   - **check the environment**: the test commands run (for example Docker is available for Testcontainers: `docker info`); record the result in `D2-PROGRESS.md`;
   - run `bash scripts/d2/install-hooks.sh` once, then `bash scripts/d2/checkpoint.sh "chore(PLT-201): bootstrap"`.
   If a skeleton piece you need does not exist, treat it as a dependency (Blocked protocol, section 4); do not build it in someone else's folder.

---

## 2. The ticket cycle (strict order)

For each ticket `<ID>`:

**A. Start**
- Run `bash scripts/d2/start-ticket.sh <ID>` (creates `d2/work` from `main` the first time, then branch `feat/<id-lowercase>`). Never commit to `main`.
- In `D2-PROGRESS.md`: status `IN_PROGRESS`, write your 8-line plan, list the slices (section 3). Then `bash scripts/d2/checkpoint.sh "chore(progress): start <ID>"`.

**B. Build in slices.** After **each slice**:
1. Run the tests of that slice (green).
2. Update `D2-PROGRESS.md` → "Last checkpoint: slice N done, next: slice N+1" and the "Resume here" block.
3. `bash scripts/d2/checkpoint.sh "feat(<ID>): <slice name>"` (stages everything, runs the lane check, commits code and progress together).

**C. Finish the ticket**
1. Add the risk-tier tests (Brief §6, Playbook §5, recipe "Tests").
2. Run the full check set (Playbook §1.3 commands as discovered in `D2-PROGRESS.md`): build, unit, integration, architecture, contract, tenant isolation, migrations from empty.
3. Walk the **per-ticket Definition of Done** (section 7). Fix anything unticked.
4. Update `D2-PROGRESS.md`: status `DONE` (or `DONE_WITH_FAKES`), commit SHA, test counts, defaults used, follow-ups, hand-off notes for whoever consumes your output.
5. `bash scripts/d2/checkpoint.sh "chore(progress): <ID> done"`.
6. `bash scripts/d2/finish-ticket.sh <ID>`: it runs the lane check, scans for skipped/ignored/pending tests, runs every configured command in `d2.conf`, checks that `D2-PROGRESS.md` says `DONE`/`DONE_WITH_FAKES` for the ticket, then merges into `d2/work` and tags `d2-<ID>-done`. **If it fails, fix the cause; never edit the script or the config to make it pass.** (If a remote and push rights exist, push branch and tags; never force-push; never touch `main`.)
7. If the ticket is subject to a review gate (section 13), **stop here** and report. Otherwise start the next ticket (step A).

**If you sense the session may end soon** (very long ticket, many tool calls, errors about limits): stop starting new work, finish or checkpoint the current slice, commit, update `D2-PROGRESS.md` with "Resume here: …" including the exact next step, then end the turn cleanly.

---

## 3. Slicing guide (keeps checkpoints small and meaningful)

Typical slices, adapt per ticket (each ends green and committed):
1. **Schema**: migration + RLS + indexes + tenant isolation test for the new tables.
2. **Domain**: aggregates / pure functions + unit and property tests.
3. **Application**: commands, validators, handlers, ports, fakes + tests.
4. **Persistence / integration**: repositories, outbox write, consumers + integration tests.
5. **API**: endpoints, permissions, problem+json, messages ar/en + endpoint tests (E1-E15).
6. **Risk tests**: concurrency, failure injection, golden, load smoke.
7. **Docs**: OpenAPI snapshot, event schemas, notes in `D2-PROGRESS.md`.

Rule of thumb: **if a slice would take more than about 30-40 tool calls, split it.**

---

## 4. Blocked protocol (never stall the whole run)

A ticket is **BLOCKED** when: a dependency (another module, `Shared`, a missing skeleton piece) is absent and cannot be faked inside your lane; or you made **3 distinct fix attempts** on the same failing test/problem without progress.

Do this:
1. Write in `D2-PROGRESS.md` → "Blocked": what, why, what you tried, exactly what is needed and from whom (architect, D1, D3, D4).
2. Commit the partial work as `wip(<id>): <state>` on its branch (it must compile; mark unfinished tests `[Trait("Category","Pending")]`, never `Skip`/`Ignore` silently).
3. Set status `BLOCKED`. Move to the next ticket in the queue whose dependencies are satisfied.
4. When the queue is exhausted, return to `BLOCKED` tickets and retry.

**Environment blockers:** if a required test type cannot run because of the environment (no Docker/Testcontainers, no database, no network to restore packages), the ticket **cannot** be `DONE`: mark it `BLOCKED (environment)` with the exact failing command and output, commit the code on its branch, and tell the human at the end of the turn. Never replace real integration tests with mocks to get green, and never mark such tests as skipped.

**Fakes instead of blocking:** if the missing piece is another module's published event or interface, build against a JSON fixture / in-memory fake in your own module's tests (Brief §0.4), finish the ticket as `DONE_WITH_FAKES`, and list exactly which fixture must be replaced by the real contract.

---

## 5. Execution queue (do in this order; skip to the next if blocked)

| # | Ticket | Why here | Needs |
|---|---|---|---|
| 1 | PLT-201 Messaging library | everything uses it | skeleton (Kernel) |
| 2 | PLT-206 Number series | **D4 is waiting** | skeleton (Persistence, RLS) |
| 3 | PLT-202 Outbox | core reliability | 1 |
| 4 | PLT-203 Worker + job runner | hosts the dispatcher | 3 |
| 5 | PLT-205 Idempotency-Key | mobile/portal safety | skeleton (Web) |
| 6 | PLT-204 Retries, dead-letter, replay | completes the outbox | 3, 4 |
| 7 | PLT-211 CI/CD gates | enforce everything | 1-6 exist |
| 8 | PLT-207 Files | Docs depends on it | 3, 4 |
| 9 | PLT-208 Approvals engine | Cost depends on it | 3 |
| 10 | PLT-209 Notifications | | 3, 4 |
| 11 | PLT-210 Excel import framework | budget import | 7 optional, 8 |
| 12 | CST-201 Budget | | 9, 2 |
| 13 | CST-206 `cost.budget.approved.v1` | tiny, unblocks Procurement | 12 |
| 14 | CST-205 Period tables + guard + close/reopen | the ledger writer needs the guard | 3 |
| 15 | CST-202 Cost ledger | | 14 |
| 16 | CST-203 Cost consumers | | 15; D4 events (fixtures OK) |
| 17 | CST-204 Earned value | | 15; D3 progress/plan events (fakes OK) |
| 18 | CSH-201 Schedules from events | | 3; D1 contracts + D4 events (fixtures OK) |
| 19 | CSH-202 13-week forecast | | 18 |

Notes: CST-205's posting-policy integration (`roll_forward` / `suspend`) is **completed in CST-202** once the writer exists; re-run CST-205's tests then. If PLT-211 conflicts with what the architect already built, extend, do not replace.

---

## 6. Default decisions (use these; record each one you use in `D2-PROGRESS.md` → "Decisions taken")

| Brief §10 | Default you apply when nobody has answered |
|---|---|
| 1 Budget approval one vs two steps | Implement **one step routed by amount** via PLT-208. Make the policy data-driven so a second step can be added without code changes to CST-201. |
| 2 Outbox ordering | Per-aggregate eligibility (`NOT EXISTS` earlier pending) + leases, as in the PLT-202 recipe. A dead-lettered event blocks later events of that aggregate until replayed. |
| 3 Worker | Use whatever job library the repo skeleton already uses. If none: the minimal hand-written runner in the PLT-203 recipe. **Add no new package.** |
| 4 Idempotency, same key in flight | `409 idempotency_in_progress` with `Retry-After: 2`. |
| 5 Number series ownership / yearly restart | If a D4 skeleton exists, **extend it** with the hardening only; otherwise build per recipe. No yearly restart; `scope_key` empty. |
| 6 CST-203 scope | Build the three required events **plus `payment.executed`** (needed for the paid golden figure). List PO changed/cancelled/closed and all reversals under **Follow-ups** unless the real schemas already include them. |
| 7 `cost.budget.revised.v1` | **Implement it** in CST-206 (additive, small); flag it in the report. |
| 8 CST-205 scope | Period tables, guard, close, reopen, snapshot on close. **No** accrual-review or forecast sign-off workflow. |
| 9 Cash settings | Settings with defaults: opening balance 0, floor 0, uncertified applications **included** with `haircut` from settings (0 if no setting exists), week starts **Sunday**, all configurable. Flag every default used. |
| 10 Ticket id mapping | Create `docs/modules/ticket-id-map.md` listing this assignment's ids; leave spec-id column `TBD`. |

A default is never a reason to stop. It is a reason to write one line in the report.

---

## 7. Per-ticket Definition of Done (paste into the ticket's section of `D2-PROGRESS.md` and tick)

- [ ] Brief requirements and the recipe "Steps" are all implemented; nothing out of scope added.
- [ ] Migrations apply from empty **and** from the previous state; RLS enabled **and forced** on every new tenant table.
- [ ] Security checklist S1-S15: every item that applies is satisfied or explicitly N/A (note which).
- [ ] Race/guard items from the Playbook §3 that apply are implemented **and** each has a test.
- [ ] Endpoint matrix E1-E15 covered for every route (those that apply).
- [ ] Risk-tier tests added (High: concurrency, failure injection, property, golden, load smoke).
- [ ] Tickets that add endpoints or tables: scenarios added to `audit/api/scenarios.yaml` (cross_tenant, idempotency, mass_assignment, etag, concurrency, single_winner, pagination_cap as they apply) and expected tables/indexes to `audit/db/expectations.yaml` (Runbook §14). If `AUDIT_CMD` is set in `d2.conf`, it passes.
- [ ] Error codes + Arabic and English messages added; OpenAPI snapshot updated; event schemas + contract tests added.
- [ ] Architecture, contract, tenant-isolation suites and the module tests are green.
- [ ] No TODOs without a ticket reference; no skipped/ignored tests; no `Pending` tests left (or ticket is `BLOCKED`/`DONE_WITH_FAKES`).
- [ ] Audit records written for state changes; logs contain no secrets or PII.
- [ ] `D2-PROGRESS.md` updated (status, SHA, tests, decisions, follow-ups, hand-off).
- [ ] `finish-ticket.sh` succeeded: merged into `d2/work` and tagged `d2-<ID>-done`.

---

## 8. Statuses (use exactly these words)

`TODO` · `IN_PROGRESS` · `DONE` · `DONE_WITH_FAKES` · `BLOCKED` · `SKIPPED` (only with a written reason from the architect).

`DONE_WITH_FAKES` requires a "Replace later" list of the exact fixtures/fakes and the real contract that must replace each.

---

## 9. Reading guide (save context)

- Read once per session: this Runbook, `D2-PROGRESS.md`, Brief §0-§6, Playbook §1-§3.
- Per ticket, read **only**: its entry in Brief §7, its Recipe, and Playbook §5 while writing tests.
- Do not re-read finished tickets' recipes. Do not load whole spec files into your context; search the local copies in `docs/specs/` (read-only) for the rule number or title you need. If `docs/specs/` is missing, record it in Open questions and rely on the Brief and recipes.

---

## 10. Report format after each ticket (also goes in `D2-PROGRESS.md`)

Use the template in Brief §9, and add: **Checkpoint:** commit SHA and tag · **Defaults used:** list · **Next ticket:** id.

---

## 11. Kickoff prompt (paste this to start or restart the agent)

```
You are D2 (Zeyad) on Construction Connect. Read, in order:
docs/team/Construction-Connect-D2-Agent-Runbook.md, then docs/team/D2-PROGRESS.md,
then the Brief and Playbook as the Runbook says.

Work through the queue in the Runbook, one ticket at a time. After each ticket:
all required tests green -> commit -> update D2-PROGRESS.md -> merge and tag -> only then start the next.
Never leave uncommitted work. Never mark DONE with a failing or skipped test.
If blocked, follow the Blocked protocol and continue with the next ticket.
If a decision is open, apply the default from Runbook section 6 and record it.
Stay inside your lane (Platform, Cost, Cash); never edit other modules, Shared, or Contracts.
Resume from the last checkpoint if D2-PROGRESS.md shows work in progress.
```

---

## 12. Standard commands (use these, do not improvise git)

| Action | Command |
|---|---|
| Once per clone (bootstrap) | `bash scripts/d2/install-hooks.sh` |
| Start a ticket | `bash scripts/d2/start-ticket.sh PLT-201` |
| Save a green slice (code + progress) | `bash scripts/d2/checkpoint.sh "feat(PLT-201): <slice>"` |
| Check you are inside your lane | `bash scripts/d2/check-lane.sh working` |
| Finish: gates, merge, tag | `bash scripts/d2/finish-ticket.sh PLT-201` |

Lane rules (enforced by the scripts and the pre-commit hook on `d2/*` and `feat/(plt|cst|csh)-*` branches):
- **Allowed:** `backend/src/Modules/{Platform,Cost,Cash}/**` and `backend/tests/Modules/{Platform,Cost,Cash}/**` (code, tests, migrations of your own modules), `docs/team`, `docs/rfc`, `docs/modules`.
- **Allowed but flagged "architect review":** `backend/src/Shared/**` (Messaging, Web, Testing, Persistence, Kernel), your modules' `*.Contracts`, `backend/src/Integrations/**`, `backend/src/Host/**` (including `AppModules.cs`), `backend/Construction.sln` (to add your new projects), the cross-cutting test projects (`backend/tests/Architecture.Tests`, `Contract.Tests`, `Tenant.Isolation.Tests`, `GoldenProject.Tests`, `E2E`, `Load`), `backend/tools/**`, `contracts/**` (event schemas, OpenAPI snapshots), `docs/adr/**`, `.github/**`, `deploy/**`, `docker-compose.yml`, and `scripts/d2/**` (only `d2.conf` command values during bootstrap; never loosen the lane rules or edit the `.sh` files to make a check pass). List every such file in `D2-PROGRESS.md` under the ticket ("Needs review").
- **Everything else is a violation** (other modules, `Directory.Packages.props`, build files, solution files). `checkpoint.sh` also refuses to commit on the base branch and refuses staged `bin/`, `obj/`, `node_modules/`. If you think you need one, write an RFC and continue with a fake or port.

If these scripts do not run on your OS (they are bash), use Git Bash or WSL; do not rewrite them.

---

## 13. Review gates (the human reviews before you continue)

Stop after the ticket is `DONE`/`DONE_WITH_FAKES` and **end your turn with a summary** (what was built, files changed, tests, decisions, risks, lane warnings) when:
- the **pilot** ticket (PLT-201) is finished, or
- the ticket is **HIGH risk**: PLT-202, PLT-205, PLT-206, PLT-208, CST-201, CST-202, CST-203, CST-204, CST-205, CSH-201, or
- `finish-ticket.sh` printed lane warnings for `backend/src/Shared/**` or any `*.Contracts`.

Write "REVIEW REQUESTED: <ID>" at the top of `D2-PROGRESS.md` → "Resume here". Continue only when the human's next message says to continue (or when the prompt explicitly contains `NO REVIEW GATES`).

---

## 14. Independent audit (human-owned, in `audit/`)

`audit/` is a black-box audit of the running API (security, response conventions, tenant isolation, idempotency, races) and of the PostgreSQL schema (RLS forced, unprivileged app role, append-only ledger, unique guards). Read `audit/README.md` once.

Your duties:
1. **Never edit anything in `audit/`** except `audit/api/scenarios.yaml`, `audit/db/expectations.yaml` and `audit/config.env`. Never weaken a check; fix the code instead.
2. For every ticket that adds endpoints or tables, **add the matching scenarios and expectations** in the same ticket (start from `audit/api/scenarios.example.yaml`; use the real routes and bodies).
3. **Provide a test-only way to obtain tokens** for tenant A (all permissions), tenant A (no permissions) and tenant B (all permissions) in the dev/test environment only (PLT-211 recipe). It must be impossible to enable in production.
4. If `AUDIT_CMD` is set in `scripts/d2/d2.conf`, `finish-ticket.sh` runs it; it must pass. If you cannot start the API or the database in your environment, say so in `D2-PROGRESS.md`; the human runs `bash audit/run_audit.sh` at each review gate.
5. A **SKIPPED** audit test means "not verified": list the skipped areas for your ticket in the report.

---

## 15. Repository facts known when these files were written (verify in bootstrap; the repo wins)

- Layout: `backend/` (solution `backend/Construction.sln`, `src/Host/{Construction.Api,Construction.Worker}` with `AppModules.cs`, `src/Modules/<Module>/<Module>.{Api,Contracts,Core,Infrastructure}`, `src/Shared/Construction.{Kernel,Messaging,Persistence,Testing,Web}`, `src/Integrations/*`, `tests/`), `contracts/{events,openapi,messages,intelligence}`, `deploy/docker/initdb/01-roles-schemas.sql`, `docker-compose.yml`, `docs/{adr,rfc,modules,runbooks}`.
- **Exists:** `Construction.Kernel` (Money, Result, Error, IClock, Ids, TenantContext, DomainRuleViolation), `Construction.Persistence` (`ModuleDbContext`, `TenantSessionInterceptor`), the Platform module with projects (Tenant aggregate, `PlatformDbContext`, migration `InitPlatform`).
- **Empty (only `.gitkeep`) today:** `Construction.Messaging`, `Construction.Web`, `Construction.Testing`, all test projects (`Architecture.Tests`, `Contract.Tests`, `Tenant.Isolation.Tests`, `GoldenProject.Tests`, per-module `*.Domain.Tests` / `*.Integration.Tests`), `.github/workflows`, the Cost and Cash module folders (**no `.csproj` yet**), `backend/tools/templates/module`.
- Consequences: (1) PLT-201 creates the Messaging project; PLT-205 uses `Construction.Web`; test helpers and `ApiFactory` go in `Construction.Testing`. (2) Cost and Cash projects must be created by you following the Platform module's project files and `ADR-001-module-entry-point.md`, then added to `backend/Construction.sln` and registered in `AppModules.cs` of Api and Worker (both are review-flagged files, keep the change minimal). (3) There is **no CI** yet and no architecture tests: the local gates in `d2.conf` and `audit/` are the safety net; creating the first architecture and tenant-isolation test projects is part of PLT-211 and PLT-202 and needs architect review. (4) New migrations go in `<Module>.Infrastructure/Persistence/Migrations`; never edit `InitPlatform`. (5) The database roles and schemas come from `deploy/docker/initdb/01-roles-schemas.sql`: read it to learn the app role name and schema names, and set `AUDIT_DB_APP_ROLE` / `AUDIT_DB_SCHEMAS` accordingly in `audit/config.env`.

