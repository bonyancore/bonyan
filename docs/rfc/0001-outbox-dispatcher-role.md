# RFC 0001 — Outbox dispatcher database role (cross-tenant claim under forced RLS)

- **Ticket:** PLT-202 (required artefact, missing at merge) · **Related:** PLT-203 (Worker host), PLT-204 (dead-letter, replay)
- **Status:** Proposed · **Needs:** architect approval + a change under `deploy/` (owner: architect / PLT-211)
- **Author:** D2 (agent) · 2026-10-31

## 1. Problem

`platform.outbox` has RLS **enabled and forced** (`ENABLE ROW LEVEL SECURITY`, `FORCE ROW LEVEL SECURITY`) with the
standard tenant policy:

```sql
USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
```

`FORCE` means the table owner is **not** exempt either. The dispatcher must, in one short transaction, claim the
pending rows of **all** tenants (it has no tenant context yet — a row's tenant is only known after it is read). With
only the tenant policy this is impossible: a claim with no `app.tenant_id` set returns zero rows, so events would
never be delivered.

We must not weaken RLS to solve this, and we must not run the Worker as the schema owner (`BYPASSRLS`/owner
connection would make the Worker able to read every tenant's data through every table, which is worse).

## 2. Proposed design — one dedicated role + one table-scoped policy

The app role(s) stay exactly as they are. A new, non-owner, non-`BYPASSRLS` role claims outbox rows and only that:

| Role | Logins | Grants |
|---|---|---|
| `cc_migrator` (exists) | deploy only | DDL, owner |
| `cc_app` (exists) | Api, Worker | DML on tenant tables, no `BYPASSRLS` |
| **`cc_outbox_dispatcher` (new)** | Worker only (separate connection string) | `SELECT`/`UPDATE` on `platform.outbox` only, plus `INSERT` on `platform.outbox_dead` (PLT-204) |

```sql
CREATE ROLE cc_outbox_dispatcher LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
GRANT USAGE ON SCHEMA platform TO cc_outbox_dispatcher;
GRANT SELECT, UPDATE ON platform.outbox TO cc_outbox_dispatcher;
GRANT INSERT ON platform.outbox_dead TO cc_outbox_dispatcher;   -- PLT-204

-- Additional PERMISSIVE policy: RLS policies are OR-ed, so this role (and only this role)
-- also sees rows its tenant policy does not match. Every other role keeps tenant isolation untouched.
CREATE POLICY outbox_dispatcher_claim ON platform.outbox
  FOR ALL
  TO cc_outbox_dispatcher
  USING (true)
  WITH CHECK (true);
```

Why this is safe:

- The policy is `TO cc_outbox_dispatcher`: no other role is affected, so S1/S2 (tenant isolation, fail closed) are
  unchanged for the Api and for every other Worker path.
- The role is not the owner and has no `BYPASSRLS`, so `FORCE ROW LEVEL SECURITY` still applies to it. It gets
  exactly the one table named in the policy, and only the DML it is granted.
- The dispatcher never touches business tables. Delivery happens **after** the claim transaction, and each delivery
  opens a separate connection **as `cc_app`** with `set_config('app.tenant_id', <row tenant>, true)`, so consumer
  effects still run fully RLS-bound (S2).
- `platform.outbox_dead` keeps its plain tenant policy; the dispatcher's `INSERT` carries the row's own `tenant_id`.

## 3. Worker wiring

Two connection strings: `ConnectionStrings:Default` (app role, business + consumers) and
`ConnectionStrings:OutboxDispatcher` (dispatcher role, claim/mark/dead-letter bookkeeping only). The dispatcher
repository (`PostgresOutboxRepository`) and the dead-letter writer (`DeadLetterService` when it runs inside the
dispatcher) use the second one; `DeadLetterService` used from the admin API uses the first (tenant-scoped).

Startup must fail closed if the dispatcher connection is missing or resolves to the same role as the app connection
in a non-development environment.

## 4. What the agent did instead (test-only workaround)

`deploy/` is review-flagged and no new role can be created by the agent, so:

- The implementation is written against a `Func<CancellationToken, Task<DbConnection>>` connection factory
  (`PostgresOutboxRepository`), so the role is a configuration decision, not a code change.
- Tests **cannot** prove the cross-tenant claim without the role, and there is no PostgreSQL in the current
  environment either. The claim/mark SQL is therefore **unproven** — recorded as `BLOCKED (environment)` and
  `Replace later` in `docs/team/D2-PROGRESS.md`. No in-memory fake is accepted as proof for this ticket.

## 5. Decision requested

1. Approve role name `cc_outbox_dispatcher` and the table-scoped policy above (architect).
2. Add the role, grants and policy to `deploy/docker/initdb/01-roles-schemas.sql` and to the production migration
   playbook (PLT-211 owns the migrator/app role split).
3. Confirm the Worker may keep a second connection pool for this role.
