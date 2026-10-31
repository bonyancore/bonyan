# D2 Recipes — Cost control (CST-201 … CST-206) and Cash flow (CSH-201 … CSH-202)

Read the Playbook first (protocol, security S1-S15, race catalogue, test matrix E1-E15). Requirements are in the Brief §7. Golden numbers: Brief §8.

Tags: `[spec]` `[rec]` `[verify]` `[decide]`. Table, route and permission names are `[rec]` until aligned with spec 08 §7, spec 10 and the real `Cost.Contracts/Permissions.cs`.

**Rules that apply to every ticket here**
- Cost and Cash never read other modules' tables. They keep their own replicas, fed by events through `Integration/Inbound` + `Acl`.
- Money is `decimal` / `numeric(18,4)`; base currency (EGP) for all reporting; **VAT never enters a measure**.
- Every route below needs: permission check (S3), project scope check (S4), validator (S6), audit (S10), problem+json errors with ar/en messages.
- Standard permission names follow `module.resource.action` (examples: `cost.budget.read|create|submit|approve`, `cost.period.close|reopen`, `cost.ledger.read`, `cash.forecast.read`) `[verify]`.

---

## CST-201 — Budget (High risk)

**Steps**
1. Migration: `cost.budgets`, `cost.budget_lines` (below), RLS (S1), indexes.
2. Aggregate `CostBudget` with states `draft → submitted → approved | rejected | cancelled`; an older approved version becomes `superseded`. Transitions in one table; invalid ⇒ `409 invalid_state_transition`.
3. Commands: `CreateDraft` (from scratch or as version `n+1` copying the current approved version, with `change_reason`), `SaveLines` (bulk), `RemoveLine`, `Submit`, `Cancel`, `OnApprovalDecided` (consumer of `platform.approval.approved/rejected.v1` for `recordType = cost.budget`).
4. Validations (BR-CST-03 `[spec]`): five category amounts ≥ 0; `line.total = sum(categories)`; `budget.total = sum(lines)`; `budget_qty × budget_unit_rate ≤ line.total`; unique `(budget, work_package, cost_code)`; cost code is a **leaf and active**; work package active and belongs to the project (check against `ref_work_packages` / `ref_cost_codes` replicas). Max lines per budget 5,000.
5. Submit: validates completeness (`cost.budget_incomplete`), requests approval from PLT-208 (amount = `total`), number from PLT-206 (`BUD-0001`).
6. Approval: in one transaction set the previous `is_current` to false and `superseded`, set the new one `approved` + `is_current = true`, publish `cost.budget.approved.v1` (CST-206).
7. Import: register `ImportBudgetHandler` with PLT-210.
8. Endpoints `[rec]`:
   - `GET  /api/v1/cost/budgets` (keyset, filter by project/status)
   - `GET  /api/v1/cost/budgets/{id}`
   - `GET  /api/v1/cost/projects/{projectId}/budget/current`
   - `POST /api/v1/cost/budgets` (create draft) · `PATCH /budgets/{id}` (header fields, `If-Match`)
   - `PUT  /api/v1/cost/budgets/{id}/lines:bulk` · `DELETE /budgets/{id}/lines/{lineId}`
   - `POST /budgets/{id}/submit` · `POST /budgets/{id}/cancel`

**Schema `[rec]`:**
```sql
CREATE TABLE cost.budgets (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, project_id uuid NOT NULL, number text NOT NULL,
  version int NOT NULL, supersedes_id uuid NULL, change_reason text NULL,
  status text NOT NULL CHECK (status IN ('draft','submitted','approved','rejected','superseded','cancelled')),
  is_current boolean NOT NULL DEFAULT false, total_amount numeric(18,4) NOT NULL DEFAULT 0,
  currency char(3) NOT NULL, created_by uuid NOT NULL, created_at timestamptz NOT NULL,
  -- optimistic concurrency uses PostgreSQL's system column xmin: map it in EF Core as the row version
  -- (do NOT create a column for it); expose it as the ETag.
  UNIQUE (tenant_id, number));
CREATE UNIQUE INDEX ux_budget_current ON cost.budgets (tenant_id, project_id) WHERE is_current;
CREATE UNIQUE INDEX ux_budget_draft   ON cost.budgets (tenant_id, project_id) WHERE status = 'draft';
CREATE UNIQUE INDEX ux_budget_version ON cost.budgets (tenant_id, project_id, version);

CREATE TABLE cost.budget_lines (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, budget_id uuid NOT NULL REFERENCES cost.budgets(id),
  work_package_id uuid NOT NULL, cost_code_id uuid NOT NULL,
  material numeric(18,4) NOT NULL CHECK (material >= 0), labour numeric(18,4) NOT NULL CHECK (labour >= 0),
  subcontract numeric(18,4) NOT NULL CHECK (subcontract >= 0), equipment numeric(18,4) NOT NULL CHECK (equipment >= 0),
  other numeric(18,4) NOT NULL CHECK (other >= 0), total_amount numeric(18,4) NOT NULL,
  budget_qty numeric(18,4) NULL, budget_unit_rate numeric(18,4) NULL, unit_code text NULL,
  UNIQUE (budget_id, work_package_id, cost_code_id));
```
(`ux_budget_version` guarantees one row per `(project, version)`; a cancelled or rejected draft keeps its version number, so the next draft uses `max(version) + 1`, computed inside the same transaction that inserts it.)

**Security:** S4 (project scope; foreign project ⇒ 404); S5 (never bind `status`, `is_current`, `version`, `number`, `total_amount`: server computes totals); only a **draft** is editable (`cost.budget_not_editable`); approval authority comes from PLT-208 (SoD, amount limit); reading budgets is a sensitive permission (margins/costs): honour `IFieldVisibility`.
**Races and guards:** two drafts ⇒ unique draft index ⇒ `cost.draft_exists`; two current versions impossible by `ux_budget_current`; flip order inside the transaction is **old false → new true**; approve is a conditional update `WHERE status='submitted'`; `PATCH`/bulk use `If-Match`; bulk save recomputes totals in SQL or in the aggregate **inside the same transaction** that writes the lines.
**Scale:** `PUT lines:bulk` is set-based (single multi-row upsert, not per-line round trips); cap at 5,000 lines; list endpoint is keyset; replica lookups are `WHERE id = ANY(@ids)`.
**Tests (High):** E1-E15 for every route; each BR-CST-03 invariant separately; FsCheck: `sum(lines) == header total` for random lines; concurrent approval of two versions ⇒ exactly one current; concurrent create draft ⇒ one wins; edit after submit ⇒ `cost.budget_not_editable`; mass assignment attempt; golden plumbing budget (BAC 1,200,000) loads and `GET .../budget/current` returns it; import with an inactive cost code ⇒ row error.
**Traps:** summing in the client; leaving the old version `is_current` when the new approval fails mid-way (single transaction!); allowing `PATCH` of `status`; using a non-leaf cost code.

---

## CST-202 — Cost ledger (High risk)

**Steps**
1. Migration: `cost.cost_entries` and the guards below; app role gets `INSERT, SELECT` only.
2. A single writer service `ILedgerWriter.Post(IEnumerable<LedgerEntry>)` used by every consumer. It: checks the period guard (CST-205), builds `posting_key`, computes `amount_base`, inserts with `ON CONFLICT (tenant_id, posting_key) DO NOTHING`, and reports how many rows were new.
3. Reversal: `ILedgerWriter.Reverse(entryId, reason, postingKey)` inserts the opposite-sign row with `reverses_entry_id`; map a unique-violation to `cost.entry_already_reversed`.
4. Query side `ILedgerReader`: totals per measure by project / work package / cost code / period; **no method returns a combined total across measures**.

**Schema `[rec]`:**
```sql
CREATE TABLE cost.cost_entries (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, project_id uuid NOT NULL,
  work_package_id uuid NULL, cost_code_id uuid NULL,
  measure text NOT NULL CHECK (measure IN ('committed','accrued','invoiced','paid')),
  entry_kind text NOT NULL,   -- commitment, commitment_consumed, commitment_release, accrual, accrual_trueup, invoice, payment, reversal ...
  amount numeric(18,4) NOT NULL, currency char(3) NOT NULL, fx_rate numeric(18,8) NOT NULL,
  amount_base numeric(18,4) NOT NULL, vat_amount_base numeric(18,4) NOT NULL DEFAULT 0,
  posting_date date NOT NULL, original_posting_date date NULL, is_late_posting boolean NOT NULL DEFAULT false,
  period_id uuid NOT NULL, source_type text NOT NULL, source_id uuid NOT NULL, source_line_id uuid NULL, source_version int NOT NULL DEFAULT 1,
  posting_key text NOT NULL, reverses_entry_id uuid NULL, group_id uuid NULL, reason text NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  CHECK (sign(amount) = sign(amount_base) OR amount = 0));
CREATE UNIQUE INDEX ux_entries_posting_key ON cost.cost_entries (tenant_id, posting_key);
CREATE UNIQUE INDEX ux_entries_reversal    ON cost.cost_entries (reverses_entry_id) WHERE reverses_entry_id IS NOT NULL;
CREATE INDEX ix_entries_wp  ON cost.cost_entries (tenant_id, project_id, work_package_id, cost_code_id, measure);
CREATE INDEX ix_entries_per ON cost.cost_entries (tenant_id, project_id, period_id, measure);

CREATE FUNCTION cost.forbid_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'cost_entries is append-only' USING ERRCODE = '42501'; END $$;
CREATE TRIGGER trg_entries_no_mutation BEFORE UPDATE OR DELETE ON cost.cost_entries
  FOR EACH ROW EXECUTE FUNCTION cost.forbid_mutation();
CREATE TRIGGER trg_entries_no_truncate BEFORE TRUNCATE ON cost.cost_entries
  FOR EACH STATEMENT EXECUTE FUNCTION cost.forbid_mutation();
REVOKE UPDATE, DELETE, TRUNCATE ON cost.cost_entries FROM app_role;
```
No `updated_*`, no `is_deleted`, **no column named total cost/spend**. `amount_base = round(amount × fx_rate, 4)` computed once in the writer.

**Security:** read access `cost.ledger.read` + project scope; cost and margin data is sensitive ⇒ field/role visibility; the trigger plus the revoked grants protect against bugs and a compromised app role; ledger writes only through `ILedgerWriter` (architecture test: no other class inserts into `cost_entries`).
**Races and guards:** idempotency is the unique `posting_key`; one reversal per entry is the partial unique index; period consistency via CST-205 (`FOR SHARE`).
**Scale:** append-only inserts are cheap; the two read indexes cover EV/budget-check queries; keep inserts batched per event; partitioning by month is a later step, not R1.
**Tests (High):** `UPDATE` and `DELETE` and `TRUNCATE` via **raw SQL as the app role** all fail; duplicate `posting_key` ⇒ one row; second reversal ⇒ `cost.entry_already_reversed`; reversal restores totals exactly; sign rules; FsCheck on `amount_base`; replay-storm (same 10,000 posting requests twice in random order ⇒ identical ledger); tenant isolation incl. raw SQL; architecture test for the single writer; load smoke (1,000 parallel postings).
**Traps:** computing `amount_base` in several places; storing VAT in `amount`; exposing a `Total` property on a ledger DTO; a "fix" script that updates rows (never).

---

## CST-203 — Cost consumers (High risk)

**Steps**
1. Confirm scope first (Brief §10 #6): minimum is PO issued, goods receipt posted, invoice matched; correct behaviour also needs payment executed, PO changed/cancelled/closed, and reversals. Build the minimum, structure the code so the rest are additional handlers.
2. In `Cost.Core/Integration/Inbound/Procurement/` add one handler per event, deriving from `IdempotentConsumer`. Translate the event in `Integration/Acl` into Cost's own `CommitmentEntry`/`ReceiptEntry`/`InvoiceEntry`. **Do not** import procurement types into the domain.
3. Each handler runs in **one transaction**: set tenant → advisory lock → period guard → build entries → `ILedgerWriter.Post` → commit (the `processed_events` row is in the same transaction).
4. Posting rules (BR-CST-11/12/13 `[spec]`):

| Event | Entries (same `group_id`) |
|---|---|
| `procurement.purchase_order.issued.v1` | `commitment`: +committed per line (ex VAT) |
| `procurement.goods_receipt.posted.v1` | `accrual`: +accrued **and** `commitment_consumed`: −committed (same value) |
| `procurement.invoice.matched.v1` | `invoice`: +invoiced; plus `accrual_trueup` so accrued equals the invoice value (ex VAT); VAT only in `vat_amount_base` |
| `procurement.payment.executed.v1` (if in scope) | `payment`: +paid |
| `procurement.purchase_order.changed/cancelled/closed.v1` (if in scope) | delta or `commitment_release` |
| receipt/invoice/payment reversed (if in scope) | reversing entries of the originals |

5. `posting_key = <source_type>:<source_id>:<source_line_id>:<source_version>:<entry_kind>` (reversals add `:<event_id>`).
6. Lock: `SELECT pg_advisory_xact_lock(hashtextextended(@project || ':' || @source_id, 0));` so versions of one PO post in order; process only if `event.version > last posted version` for that source, else ignore as stale.
7. Unknown work package/cost code ⇒ post as **unallocated** (nulls) and raise a data-quality alert; never drop the event.
8. Contract: write the consumer against the **publisher's JSON schema** (D4's `PRC-408/409/410`). Until it exists, use a fixture file and a fake publisher; add the consumer-side contract test now and switch fixtures to real schemas later.

**Security:** the consumer trusts only the envelope's `tenantId` (set the tenant context from it, S2) and verifies the referenced project belongs to that tenant (RLS does this); never trust amounts from outside the event; log ids only.
**Races and guards:** see Playbook §3 rows "double delivery", "out of order", "PO versions", "posting while closing". Both the unique `posting_key` and the version check must exist; neither alone is enough.
**Scale:** one event ⇒ a handful of inserts; keep the advisory lock scope to `(project, source)` so unrelated posting is parallel; batch lines of one event in one multi-row insert.
**Tests (High):** cement scenario (committed +179,340; receipt consumes the commitment and accrues the same value); invoice 181,200 vs receipt 180,000 ⇒ invoiced +181,200, true-up +1,200, accrued 181,200, `vat_amount_base` 25,368; payment ⇒ paid (reach 300,000 in the plumbing golden if payment is in scope); duplicate delivery ×10 concurrent ⇒ one effect; out-of-order versions; unallocated line; closed-period receipt under `roll_forward`; failure injected before commit ⇒ no partial ledger and the event retried; consumer contract test against the schema; replay-storm.
**Traps:** using the event's total for a multi-line PO instead of line amounts; booking VAT in a measure; non-transactional `processed_events`; throwing away events for unknown codes.

---

## CST-204 — Earned value (High risk)

**Steps**
1. Implement `EarnedValueCalculator` as a **pure static function** in `Cost.Core/Domain` (no I/O, no clock). Skeleton:
```csharp
public sealed record EvInput(decimal Bac, decimal PlannedPct, decimal ActualPct, decimal Ac);
public sealed record EvResult(decimal Pv, decimal Ev, decimal Ac, decimal Cv, decimal Sv,
    decimal? Cpi, decimal? Spi, decimal? Eac, decimal? Etc, decimal? Vac, decimal? Tcpi, string EacMethod);

public static EvResult Calculate(EvInput i)
{
    var pv = i.Bac * i.PlannedPct;              // percentages as fractions 0..1
    var ev = i.Bac * i.ActualPct;
    decimal? cpi = i.Ac == 0 ? null : ev / i.Ac;
    decimal? spi = pv == 0 ? null : ev / pv;
    string method; decimal? eac;
    if (i.ActualPct >= 0.05m && cpi is > 0) { eac = i.Bac / cpi; method = "cpi"; }
    else { eac = i.Ac + (i.Bac - ev); method = "budget_rate"; }
    var etc = eac - i.Ac; var vac = i.Bac - eac;
    decimal? tcpi = (i.Bac - i.Ac) == 0 ? null : (i.Bac - ev) / (i.Bac - i.Ac);
    return new(pv, ev, i.Ac, ev - i.Ac, ev - pv, cpi, spi, eac, etc, vac, tcpi, method);
}
```
Keep full precision (`decimal`, 8 dp stored); round **only for display**.
2. Inputs assembled by an application service: `BAC` from the current approved budget; `PlannedPct` from `wp_planned_curve` (current baseline, linear interpolation at the status date); `ActualPct` from the latest **verified** `wp_progress_points` on or before the status date (unverified progress is never used); `AC` = accrued from the ledger (base currency, cumulative).
3. Roll-up: parent work packages and the project **sum** leaf EV, AC and PV, then compute indices from the sums. Never average CPI/SPI.
4. Persist `cost.ev_snapshots(tenant_id, project_id, work_package_id, status_date, formula_version='ev-1.0', inputs jsonb, result jsonb, progress_stale boolean)` upserted by `(tenant, project, wp, status_date, formula_version)`. `progress_stale = true` when the latest verified point is older than the configured days.
5. Replicas `wp_progress_points` and `wp_planned_curve` are fed by D3's events (`planning.progress.verified.v1`, `planning.schedule.baselined/revised.v1`): until those exist, seed them with fakes and say so in the report.
6. Endpoints `[rec]`: `GET /api/v1/cost/projects/{projectId}/earned-value?statusDate=` and `.../work-packages/{id}/earned-value`.

**Security:** cost and margin data ⇒ S3/S4 and field visibility; the endpoint accepts only a date, never raw numbers; results are stored with their inputs for traceability (BR-X-10).
**Races:** the calculator is pure (no race). The snapshot upsert is idempotent by its key; a recompute job uses the advisory lock per `(tenant, project)` and a single read snapshot (Playbook §3).
**Scale:** compute from indexed ledger sums; if the project p95 target is missed, add a rollup read model (Playbook §4.11); cache per `(project, status_date)` invalidated by ledger/progress events.
**Tests (High):** **golden**: BAC 1,200,000, planned 50%, actual 40%, AC 540,000 ⇒ PV 600,000, EV 480,000, CV −60,000, SV −120,000, CPI 0.8889 (stored 0.88888889), SPI 0.8000, EAC 1,350,000, ETC 810,000, VAC −150,000, TCPI 1.0909; roll-up (EV 100/AC 125/PV 100 + EV 300/AC 250/PV 200 ⇒ CPI 1.0667, SPI 1.3333, not the average); `AC = 0` ⇒ null CPI and no exception; EAC method switches at 5%; unverified progress ignored; stale-progress flag; FsCheck (`ETC = EAC − AC`, `VAC = BAC − EAC`); permission and project-scope tests (E2-E5).
**Traps:** using reported (unverified) progress; rounding before dividing; averaging child indices; using committed or invoiced as AC.

---

## CST-205 — Period close (High risk)

**Steps**
1. Tables `cost.cost_periods` and `cost.posting_suspense` (below). A period = a calendar month in the **project timezone**.
2. State machine `open → accruals_reviewed → forecast_updated → closed → reopened`. Implement what the ticket needs: the **guard**, `close`, and `reopen` with reason. The accrual-review and forecast sign-off workflow is in spec 08 but **not** in this ticket (Brief §7 CST-205) `[decide]`.
3. `ICostPeriodGuard.IsOpen(projectId, date)` (public contract for Procurement): reads the period with `SELECT ... FOR SHARE`.
4. `ILedgerWriter` calls the guard inside the posting transaction. Closed period ⇒ apply `late_posting_policy`: **`roll_forward`** (default: post in the earliest open period, keep `original_posting_date`, set `is_late_posting = true`, raise a `late_posting` alert) or **`suspend`** (insert into `cost.posting_suspense` until reopen/resolve).
5. `POST /api/v1/cost/periods/{id}/close` (SoD: whoever prepared it cannot close it, `cost.period_sod`) and `POST /periods/{id}/reopen` (permission `cost.period.reopen`, reason mandatory `cost.reason_required`, only the **latest** closed period unless later ones are reopened first). Re-close increments `snapshot_version` and keeps older snapshots. Record all in the audit log.
6. Snapshot on close: store the totals per measure for the period (`cost.period_snapshots`) so reports are reproducible.

**Schema `[rec]`:**
```sql
CREATE TABLE cost.cost_periods (
  id uuid PRIMARY KEY, tenant_id uuid NOT NULL, project_id uuid NOT NULL, year int NOT NULL, month int NOT NULL,
  status text NOT NULL CHECK (status IN ('open','accruals_reviewed','forecast_updated','closed','reopened')),
  prepared_by uuid NULL, closed_by uuid NULL, closed_at timestamptz NULL, snapshot_version int NOT NULL DEFAULT 0,
  UNIQUE (tenant_id, project_id, year, month));
```
**Security:** reopen is a high-privilege action (S3, S10, S11): permission + reason + audit, and an alert to finance; the guard runs server-side inside the posting path, so no client can bypass it; replayed events cannot bypass it (the guard is in the writer).
**Races and guards:** posting does `FOR SHARE` on the period row before inserting; `close` takes an exclusive lock via `UPDATE ... WHERE status = <expected>` so it waits for in-flight postings and later postings see `closed`. Concurrent close attempts: conditional update, second gets 409.
**Scale:** period rows are few; the lock is per `(project, period)`, so other projects are unaffected.
**Tests (High):** `late-receipt-closed-month` under both policies; posting and close started together ⇒ no entry in a closed period; reopen without reason ⇒ 422; reopen out of order ⇒ error; SoD (preparer cannot close); re-close creates `snapshot_version = 2` and keeps version 1; replayed event cannot post into a closed period; permission matrix (E2-E5); audit rows written.
**Traps:** checking `IsOpen` in a separate transaction before posting (TOCTOU); computing period boundaries in UTC; editing an old snapshot.

---

## CST-206 — Event `cost.budget.approved.v1` (Medium risk)

**Steps**
1. Add the C# record in `Cost.Contracts/Events/` and the JSON schema `cost.budget.approved.v1.schema.json` in `contracts/events/` (repo root; follow `contracts/README.md`):
```
payload: budgetId, projectId, version (=1), totalAmount, currency,
         lines[{ workPackageId, costCodeId, totalAmount, categoryAmounts{material,labour,subcontract,equipment,other}, budgetQty?, unitCode? }]
```
2. Publish through `IOutbox` in the **same transaction** as the approval (CST-201 step 6), `aggregate_id = budgetId`, `aggregate_version = version`.
3. Decide with the architect whether versions ≥ 2 publish `cost.budget.revised.v1` here (Brief §10 #7). Without it, Procurement's budget-check table goes stale after any revision.
4. Do **not** write Procurement's consumer; hand over the schema and a sample payload file (`contracts/events/samples/` if the folder convention allows it, otherwise in the schema file's `examples`).

**Security:** the payload carries ids and amounts only; it is sent only after approval; tenant comes from the envelope; no names or free text.
**Races:** same-transaction publication guarantees "approved ⇔ event exists"; consumers handle redelivery and order via `version`.
**Scale:** a budget of 5,000 lines produces a large payload: set a max payload size check `[verify limit]`; if exceeded, publish a reference event with a pull query `[decide]`.
**Tests:** approval emits exactly one event; rollback emits none; rejection emits none; payload validates against the schema and matches the golden budget; a consumer fixture (Procurement stand-in) can parse it; contract test fails when a required field is removed.
**Traps:** publishing after commit (loses events); changing a field in `v1` instead of adding a new version.

---

## CSH-201 — Receivable and payable schedules fed by events (High risk)

**Steps**
1. Migration for schema `cash` (the name is `cash`, not `cashflow`): `cash.contract_refs`, `cash.receivables`, `cash.payables`, `cash.forecast_dirty` (names `[rec]`), RLS, indexes.
2. One handler per event in `Cash.Core/Integration/Inbound/<Publisher>/`, deriving from `IdempotentConsumer`; translate through `Integration/Acl`. Consumed events (see spec 10 §8.2 for the authoritative list and handler rules `[verify]`): `contracts.contract.signed.v1`, `contracts.payment_application.submitted.v1`, `contracts.payment_application.certified.v1`, `contracts.invoice_to_client.issued.v1`, `contracts.receipt.recorded.v1`, procurement `invoice.matched` / `payment.scheduled` / `payment.executed`, and `cost.budget.*`/`cost.period.closed` as the spec requires.
3. Receivable rules `[spec]`:
   - `submitted` ⇒ **expected** receivable: `gross = payableAmount × (1 − haircut)`, `due_date = expectedCertificationDate + payment_terms_days`, confidence **medium**.
   - `certified` ⇒ **certified** receivable with the certified amounts, confidence **high**; replaces/links the expected one.
   - `invoice_to_client.issued` ⇒ links to the certificate receivable.
   - A late `submitted` must not downgrade an existing certified row (`[verify]` the exact BR in spec 10).
4. Natural-key upsert with a version guard:
```sql
INSERT INTO cash.receivables (tenant_id, source_type, source_id, source_version, ...)
VALUES (...)
ON CONFLICT (tenant_id, source_type, source_id) DO UPDATE
SET ... , source_version = EXCLUDED.source_version
WHERE cash.receivables.source_version < EXCLUDED.source_version;   -- ignore older events (BR-CSH-27)
```
5. In the **same transaction** as the upsert: insert `processed_events`, mark the affected `(tenant, project)` forecast `is_dirty = true`.
6. Failures follow PLT-204 retry/dead-letter. Until D1's contracts events exist, build against JSON fixtures and a fake publisher.

**Security:** tenant from the envelope (S2); amounts come only from events; nobody can write schedules through an endpoint in R1 (read-only API); schedule reads need `cash.forecast.read` + project scope; cash positions are highly sensitive ⇒ field visibility.
**Races and guards:** unique natural key + version guard handle duplicates and out-of-order; the dirty flag and the row change commit together; recomputation is serialized by an advisory lock per `(tenant, project)` (CSH-202).
**Scale:** one small upsert per event; index `(tenant_id, project_id, due_date)`; avoid recomputing the forecast synchronously in the consumer, mark dirty and let the Worker recompute (debounced, e.g. at most once per project per minute `[rec]`).
**Tests (High):** golden PA-04 (gross 2,445,500; advance recovery 244,550; retention 122,275; deductions 15,000; net 2,063,675; VAT 288,915; payable 2,352,590): submitted creates the expected receivable with the right amounts and confidence; certified upgrades it; duplicate ×10 concurrent ⇒ one row; out-of-order (certified then submitted) ⇒ stays certified; event with an older version ignored; failure injected after upsert before commit ⇒ neither row nor dirty flag; consumer contract tests against publisher schemas; tenant isolation; permission matrix on the read endpoints.
**Traps:** computing the forecast inside the consumer; using the submitted amount after certification; reading contract tables to "complete" missing data; timezone bugs on `due_date`.

---

## CSH-202 — 13-week cash forecast view (Medium risk)

**Steps**
1. Read model `cash.rm_forecast_weeks(tenant_id, project_id?, run_id, week_start, inflow, outflow, net, closing_balance, confidence_breakdown jsonb, as_of)` and a pointer table `cash.forecast_runs(id, tenant_id, scope, status, computed_at, is_current)`.
2. Recompute job (Worker): pick dirty scopes, take `pg_try_advisory_lock(hashtext(tenant||scope))`, open a `REPEATABLE READ` transaction, compute 13 weekly buckets from `cash.receivables` and `cash.payables`, write rows under a new `run_id`, then flip `is_current` in one statement; readers always see a complete run.
3. Week boundaries use the **project/tenant timezone**; the first day of the week is a decision `[decide]` (Core spec vs v3 conflict was noted for Planning; confirm for Cash).
4. Opening balance, minimum-balance floor, haircut on uncertified applications, and whether uncertified items appear in the base case are **open questions** (Brief §10 #9): put them in tenant/project settings with safe defaults (opening balance from settings, floor 0, uncertified included with the configured haircut) and document the defaults.
5. Shortfall detection: first week where `closing_balance < floor`; publish `cash.shortfall.detected.v1` **only on transition** (state stored on the run/scope); publish `cash.forecast.refreshed.v1` after each successful run.
6. Endpoints `[rec]`: `GET /api/v1/cash/projects/{projectId}/forecast?weeks=13` (returns weekly rows, `as_of`, `firstShortfallWeek`); company-level `GET /api/v1/cash/forecast` if the spec requires it `[verify]`.

**Security:** S3/S4; company-level view needs a higher permission than the project view; response omits hidden fields; no endpoint accepts balances or amounts as input.
**Races:** advisory lock + run pointer (no partial reads); shortfall de-duplication by stored state; the job is idempotent (recompute twice ⇒ same output, one event).
**Scale:** 13 weeks × scopes is small; recompute is bounded by schedule rows per scope; debounce; reads hit `rm_*` only (p95 < 2 s `[spec]`).
**Tests:** seeded schedules ⇒ expected weekly totals, closing balances and first-shortfall week; boundary: item due on the last second of a week; opening-balance variants; recompute twice ⇒ identical rows and a single shortfall event; concurrent recompute ⇒ one runs; reader during recompute sees the previous complete run; `as_of` present; incremental result equals full rebuild; E1-E8 and E12-E14 on the endpoint.
**Traps:** summing both expected and certified versions of the same application (double count); mixing time zones; publishing the shortfall event on every recompute.
