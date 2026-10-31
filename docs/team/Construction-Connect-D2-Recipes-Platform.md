# D2 Recipes — Platform services (PLT-201 … PLT-211)

Read the Playbook first (protocol, security S1-S15, race catalogue, test matrix E1-E15). Requirements are in the Brief §7. This file gives **step-by-step implementation, schema, endpoints, guards and tests**.

Tags: `[spec]` `[rec]` `[verify]` `[decide]` as in the Playbook. Table and route names are `[rec]` until aligned with the repo.

Each recipe: **Steps · Schema · API · Security · Races and guards · Scale · Tests · Traps.**

---

## PLT-201 — Messaging library (Low risk)

**Steps**
1. In `Construction.Messaging` (architect-owned: PR + review) add `EventEnvelope<TPayload>` (fields in Brief §2.5), `IOutbox`, `IEventHandler<TEvent>`, `IntegrationEvent` marker, `EventTypeAttribute("module.aggregate.verb.v1")`.
2. Add `IdempotentConsumer<TEvent>` base: opens a transaction → `INSERT INTO platform.processed_events(consumer, event_id) ON CONFLICT DO NOTHING` → if 0 rows inserted, return (duplicate) → else run `HandleAsync` in the same transaction → commit.
3. Add helpers to load a JSON schema and validate a payload (used by contract tests).
4. Keep it free of business rules and of module references (only `Kernel`).

**Schema (owned by Platform):**
```sql
CREATE TABLE platform.processed_events (
  consumer text NOT NULL, event_id uuid NOT NULL, processed_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (consumer, event_id));   -- not tenant-scoped: event ids are globally unique
```
**Security:** envelope carries `tenantId`; the consumer base **sets the tenant context from the envelope** before running the handler (S1/S2) and rejects an envelope without `tenantId`.
**Races:** the primary key is the guard; no application lock.
**Scale:** `processed_events` grows; add a retention job (delete rows older than 30 days `[rec]`) in PLT-203/204 housekeeping.
**Tests:** duplicate event skipped; handler exception → row **not** kept (transaction rolled back); missing tenant → rejected; architecture: `Messaging` references only `Kernel`.
**Traps:** do not call `SaveChanges` outside the consumer's transaction; do not catch handler exceptions inside the base class.

---

## PLT-202 — Outbox (High risk)

**Steps**
1. Migration for `platform.outbox` (below). Append-only except dispatch bookkeeping columns.
2. Outbox writer in `Construction.Persistence`: `IOutbox.Publish(evt)` adds a row **via the same `DbContext`/transaction** as the business change. No separate connection.
3. Worker dispatcher loop (below): claim → deliver → mark. Wire consumers through DI by event type.
4. Metrics: `outbox_lag_seconds` (now − oldest undispatched `occurred_at`), `outbox_pending`, `outbox_attempts`.
5. Housekeeping job: delete dispatched rows older than the retention window in batches.

**Schema `[rec]`:**
```sql
CREATE TABLE platform.outbox (
  id uuid PRIMARY KEY,                       -- uuid v7 = event id
  tenant_id uuid NOT NULL,
  aggregate_type text NOT NULL, aggregate_id uuid NOT NULL, aggregate_version bigint NOT NULL,
  type text NOT NULL, payload jsonb NOT NULL, occurred_at timestamptz NOT NULL,
  attempts int NOT NULL DEFAULT 0, next_attempt_at timestamptz NOT NULL DEFAULT now(),
  locked_by text NULL, locked_until timestamptz NULL,
  dispatched_at timestamptz NULL, dead_at timestamptz NULL);
CREATE INDEX ix_outbox_pending ON platform.outbox (next_attempt_at, id) WHERE dispatched_at IS NULL AND dead_at IS NULL;
CREATE INDEX ix_outbox_agg ON platform.outbox (aggregate_id, id) WHERE dispatched_at IS NULL AND dead_at IS NULL;
```
RLS applies for API writes; the Worker dispatcher runs with a **system role that can read across tenants only for this table** `[decide]`, and sets the tenant per event before delivery.

**Claim query (short transaction):**
```sql
WITH cand AS (
  SELECT o.id FROM platform.outbox o
  WHERE o.dispatched_at IS NULL AND o.dead_at IS NULL
    AND o.next_attempt_at <= now()
    AND (o.locked_until IS NULL OR o.locked_until < now())
    AND NOT EXISTS (                              -- per-aggregate ordering
      SELECT 1 FROM platform.outbox p
      WHERE p.aggregate_id = o.aggregate_id AND p.id < o.id
        AND p.dispatched_at IS NULL AND p.dead_at IS NULL)
  ORDER BY o.id LIMIT @batch
  FOR UPDATE SKIP LOCKED)
UPDATE platform.outbox o SET locked_by=@me, locked_until=now()+interval '60 seconds'
FROM cand WHERE o.id = cand.id RETURNING o.*;
```
Deliver each row **outside** the claim transaction; then in a second short transaction set `dispatched_at = now()` (success) or `attempts = attempts+1, next_attempt_at = <backoff>, locked_*=NULL` (failure). A dead-lettered earlier event **blocks** later events of the same aggregate until replayed or skipped by an admin `[decide]`.

**Security:** payloads must not contain secrets or file contents; events carry ids and business facts only. The dispatcher must set the envelope's tenant context for each delivery (S2).
**Races and guards:** see Playbook §3 rows "two dispatchers", "per-aggregate order", "crash after side effect". `aggregate_version` is assigned by the aggregate while it holds its row lock, so consumers can detect gaps and old versions; do **not** rely on a global sequence for commit order.
**Scale:** partial indexes keep the pending set small; batch 50-200; retention job; lag alert at 60 s.
**Tests (High):** (1) rollback ⇒ no row; (2) commit ⇒ exactly one row; (3) N parallel dispatchers ⇒ each event's **effect** once; (4) kill after handler before mark ⇒ redelivery, one effect; (5) three aggregates interleaved ⇒ per-aggregate order; (6) lag metric rises when the dispatcher is stopped; (7) load smoke: 5,000 events drain with no loss.
**Done when `[spec]`:** a test event reaches a test consumer exactly once.
**Traps:** delivering inside the claim transaction (long locks); using `ORDER BY occurred_at` (clock skew) instead of id/version; forgetting the lease so a crashed worker's rows stay locked forever.

---

## PLT-203 — Worker host and job runner (Low risk)

**Steps**
1. `Construction.Worker` host registering modules via `AppModules.cs`; only modules switched on register their jobs/consumers.
2. Job abstraction: `IJob { string ModuleCode; Task RunAsync(JobContext ctx, CancellationToken ct); }`. `JobContext` carries tenant, run id, `IClock`.
3. Queues: `critical`, `default`, `reports`, `collectors`, `ai`. Per-tenant enable flag. Cron in the tenant's timezone.
4. `platform.job_runs(id, tenant_id, job, queue, started_at, finished_at, status, error_code, attempts)`; write start and end.
5. Hard timeout per job via a linked `CancellationTokenSource`; a timed-out job is marked `timed_out`.
6. Decide Hangfire (PostgreSQL storage) vs a small hand-written runner in an ADR `[decide]`.

**Security:** jobs run with a tenant context and RLS like requests (S1/S2); a job that has no tenant fails closed. Jobs never accept arbitrary parameters from the network; the replay/admin endpoints pass ids only.
**Races:** a scheduled job must not overlap itself per tenant: `pg_try_advisory_lock(hashtext(job||tenant))`; if it fails, record `skipped`.
**Scale:** stateless workers, N instances, queues limit concurrency; per-queue parallelism configurable.
**Tests:** job writes `job_runs`; timeout enforced (fake clock + long job); two workers ⇒ the job runs once; no tenant ⇒ exception; module switched off ⇒ its jobs are not registered.
**Traps:** using `Task.Delay` in tests; running jobs under the owner DB role.

---

## PLT-204 — Retries, dead-letter, replay (Medium risk)

**Steps**
1. Backoff schedule (config): attempt 1 → +10 s, 2 → +1 m, 3 → +5 m, 4 → +30 m, 5 → +2 h; after the 5th failure set `dead_at`, copy the row to `platform.outbox_dead` (event, last error, attempts) and raise a metric/alert.
2. Store the **error category and message summary** (no payload dumps with PII).
3. Endpoint (admin): `POST /api/v1/platform/outbox/dead/{id}/replay` → creates a fresh pending outbox row (new attempt chain) and marks the dead row `replayed_at`. `GET /api/v1/platform/outbox/dead` with keyset pagination.
4. Audit every replay (who, why).

**Schema:** `platform.outbox_dead(id, original_event_id, tenant_id, type, payload jsonb, attempts, last_error text, dead_at, replayed_at, replayed_by)`.
**Security:** replay and list require a platform-admin permission `platform.outbox.replay` `[verify code]`; rate-limited (S12); the list view must not leak other tenants (tenant-scoped RLS, or a system-admin permission that is explicitly cross-tenant `[decide]`).
**Races:** replay is a conditional update (`WHERE replayed_at IS NULL`); two clicks ⇒ one replay.
**Scale:** dead table is small; index `(tenant_id, dead_at)`.
**Tests:** retry schedule with a fake clock (exact delays); 5th failure ⇒ dead row + alert; replay succeeds after the fix and the effect happens once; replay twice ⇒ second is 409; permission/tenant matrix (E2-E5); audit written.
**Traps:** retrying forever; resetting `attempts` and hiding a poison message; replay without audit.

---

## PLT-205 — Idempotency-Key middleware (High risk)

**Steps**
1. Middleware for `POST` endpoints that opt in (mandatory for mobile/portal create endpoints): read `Idempotency-Key` (UUID); if missing on a mandatory endpoint ⇒ `400 idempotency_key_required`.
2. Compute `request_hash = SHA256(method + normalized path + canonical body)`. Limit body size.
3. `INSERT ... ON CONFLICT DO NOTHING` the key row as `in_progress` **before** running the handler.
4. If the insert succeeded ⇒ run the handler; on 2xx/4xx store status, headers (safe subset) and body, mark `completed`; on exception/5xx **delete** the row so a retry can run.
5. If the row already exists: hash differs ⇒ `422 idempotency_key_reuse`; `in_progress` and not expired ⇒ `409 idempotency_in_progress` (`Retry-After: 2`); `completed` ⇒ replay the stored response with header `Idempotency-Replayed: true`.
6. Expired `in_progress` rows (older than 60 s) are treated as abandoned and may be taken over. Completed rows expire after 24 h; a housekeeping job deletes them.

**Schema `[rec]`:**
```sql
CREATE TABLE platform.idempotency_keys (
  tenant_id uuid NOT NULL, user_id uuid NOT NULL, key uuid NOT NULL,
  request_hash bytea NOT NULL, state text NOT NULL CHECK (state IN ('in_progress','completed')),
  response_status int NULL, response_body jsonb NULL, created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (tenant_id, user_id, key));
```
**Security:** scoped to `(tenant, user)` so one user cannot replay another's response (S13). Stored bodies can contain business data: RLS applies, 24 h retention, never logged. Do not store `Authorization` or cookies.
**Races:** the primary key insert is the lock. See Playbook §3 row "same request retried".
**Scale:** one extra insert and one update per create; housekeeping in batches.
**Tests (High):** same key+body ⇒ identical response and one DB row; same key + different body ⇒ 422; 10 parallel identical requests ⇒ one execution; handler throws ⇒ key removed and retry executes; expiry at 24 h (fake clock); other user same key ⇒ fresh execution; missing key on a mandatory endpoint ⇒ 400.
**Traps:** hashing only the body (path/method must be included); caching 5xx; storing the response before the business transaction commits in a way that outlives a rollback.

---

## PLT-206 — Number series (High risk, ship early)

**Steps**
1. Migration for `platform.number_series`; seed default series per tenant on tenant creation or lazily.
2. `INumberSeries.Next(tenantId, seriesCode, scopeKey = "")` executes inside the **caller's** transaction (no new connection):
```sql
INSERT INTO platform.number_series(tenant_id, series_code, scope_key, prefix, padding, next_value)
VALUES (@t,@s,@k,@prefix,@pad,1) ON CONFLICT DO NOTHING;
UPDATE platform.number_series SET next_value = next_value + 1
WHERE tenant_id=@t AND series_code=@s AND scope_key=@k
RETURNING prefix, padding, next_value - 1 AS issued;
```
3. Format: `prefix + zero-pad(issued, padding)` (e.g. `PO-1066`). Return both the numeric and the formatted value.
4. Document for D4: call `Next()` as late as possible in the transaction, never in a read-only path.
5. Admin endpoint to read/update `prefix` and `padding` only (never `next_value`) `[decide]` whether needed in R1.

**Schema:** `platform.number_series(tenant_id, series_code, scope_key, prefix, padding, next_value bigint, PRIMARY KEY (tenant_id, series_code, scope_key))`. Target tables need `UNIQUE (tenant_id, number)` as a second guard.
**Security:** RLS; the counter is never accepted from input; changing `prefix` affects only future numbers.
**Races and guards:** the `UPDATE` holds a row lock until the caller commits, so numbers are gapless and a rollback returns the number. Concurrent creators of the same series **serialize**, by design.
**Scale:** a hot row per series. Keep the transaction short; measure; if one series becomes a bottleneck, per-project `scope_key` splits the load (series that restart by year/project is an open question).
**Tests (High):** 50 parallel creates ⇒ unique, gapless; rollback ⇒ number reused; two tenants independent; two series independent; prefix/padding formatting; a creator that fails after `Next()` leaves no gap; load smoke 500 parallel.
**Traps:** using a database `SEQUENCE` (not gapless, not rolled back); calling `Next()` early and doing slow work after it; reading then writing (read-modify-write race).

---

## PLT-207 — Files (Medium risk; Docs depends on it)

**Steps**
1. `IFileService` port in Platform; S3-compatible adapter in `backend/src/Integrations/Integrations.Storage` (architect-owned folder) `[rec]`; MinIO for dev/tests.
2. `POST /api/v1/platform/files/uploads` body `{ originalName, contentType, size, linkedEntityType?, linkedEntityId? }` → validate (below) → create row `status='pending'`, `storage_key = {tenant}/{yyyy}/{file_id}` (**never** derived from the user's file name) → return `{ fileId, uploadUrl (pre-signed PUT, 10 min), headers }`.
3. Client uploads directly to storage. Then `POST /files/{id}/complete` (idempotent): server does a `HEAD` on the object, verifies size and a **magic-byte sniff** of the first bytes against the declared type; mismatch or oversize ⇒ delete the object, mark `rejected`, return 422.
4. Enqueue `scan` job; file is **unavailable** until `scan_status = clean`. Thumbnail job for images.
5. `GET /files/{id}/download` ⇒ permission + project scope check ⇒ short-lived (5 min) pre-signed GET with `response-content-disposition: attachment; filename*=UTF-8''...`; audit the download.
6. Janitor job removes `pending` rows and objects older than 24 h.

**Schema `[rec]`:** `platform.files(id, tenant_id, storage_key, original_name, content_type, size_bytes, sha256, status, scan_status, uploaded_by, linked_entity_type, linked_entity_id, created_at)`; index `(tenant_id, linked_entity_type, linked_entity_id)`.
**Security:**
- Allow-list of types (images, PDF, Office, DWG/DXF zipped, CSV); block executables, HTML, SVG and anything not on the list. Limits: 25 MB default, 100 MB for drawings `[spec]`.
- Sanitize `originalName` for display (strip path separators and control chars); keep the Arabic name in the DB, encode it in `filename*` for downloads.
- Serve with `Content-Disposition: attachment` and `X-Content-Type-Options: nosniff`.
- Pre-signed URLs are secrets: never log them, short expiry, bound to the exact key and (for PUT) the content type.
- A pre-signed PUT cannot enforce size by itself ⇒ the `complete` check is mandatory (S15).
- Cross-tenant: object keys are prefixed by tenant, rows are RLS-protected, and download re-checks the row's tenant and project access (S4).
**Races:** `complete` is a conditional transition `pending → uploaded` (`WHERE status='pending'`); calling it twice is a no-op. A file that is replaced gets a new row; old rows are never overwritten.
**Scale:** direct-to-storage upload keeps the Api out of the data path; scan and thumbnails run in the Worker queue; keep metadata queries index-backed.
**Tests:** E1-E8; oversize and wrong-type at request time and at `complete`; declared `image/png` but PDF bytes ⇒ rejected; `complete` without an object ⇒ 422; `complete` twice ⇒ idempotent; download before scan clean ⇒ 409/423 `file_not_ready`; download from another tenant/project ⇒ 404; Arabic file name round-trip; headers `nosniff` and attachment present; janitor deletes stale pending; audit on download.
**Traps:** building the storage key from the file name (path traversal, collisions); trusting `contentType` from the client; returning the storage URL directly in list responses.

---

## PLT-208 — Approvals engine (High risk)

**Steps**
1. Tables: `platform.approval_policies` (record type, min/max amount, optional project/trade, approver role or user, amount limit) and `platform.approval_instances` (record ref, amount, requested_by, status, decided_by, decided_at, reason).
2. `IApprovalService.Request(recordRef, amount, projectId, trade?)`: picks the policy by amount/project/trade; if none matches ⇒ error `platform.approval_no_policy`; creates **one pending instance per record** (partial unique index `WHERE status='pending'`).
3. `Decide(instanceId, approve|reject, reason)`:
   - conditional update `WHERE status='pending'` (rows affected must be 1);
   - checks: caller has the approver permission **and** the amount limit covers it; `caller != requested_by` unless the tenant flag allows (BR-X-07 `[spec]`);
   - publish `platform.approval.approved.v1` / `rejected.v1` via outbox in the same transaction.
4. Endpoints `[rec]`: `GET /api/v1/platform/approvals/pending` (mine), `POST /approvals/{id}/approve`, `POST /approvals/{id}/reject` (reason mandatory on reject).
5. Owning modules react by `recordType` (e.g. `cost.budget`). You do **not** edit their code; you publish the event and document the contract.

**Out of scope (R1 lite):** multi-step chains, delegation, escalation `[spec]`.
**Security:** SoD on `user_id` (S11); approver must have project access (S4); reject requires a reason; decisions are audited; the amount used for routing is read from the record by the owning module through the request call, **never** from the client body.
**Races:** conditional update as above; unique pending index prevents two instances per record; approval and event are one transaction.
**Scale:** pending list is indexed by `(tenant_id, approver_id, status)`; policy lookup is cached per tenant with invalidation on change.
**Tests (High):** boundary amounts at each limit (`=limit`, `limit+0.0001`); no matching policy; self-approval blocked (+ tenant-exception test); approver without the project ⇒ 404; two approvers decide at once ⇒ one wins, other 409; reject without reason ⇒ 422; second `Request` for the same record ⇒ 409; outbox event exists exactly once and none on failure; E2-E5 for all endpoints.
**Traps:** routing by a role name instead of the amount limit; trusting the amount from the client; allowing `approve` after `cancelled`; forgetting the one-step vs two-step conflict (Brief §10 #1).

---

## PLT-209 — Notifications (Medium risk)

**Steps**
1. `INotificationService.Send(NotificationIntent)`; an intent has `templateCode`, `recipients` (user ids), `params`, `link`, `dedupeKey?`, `category` (`approval`, `security`, `info`).
2. Templates table `platform.notification_templates(code, locale, version, subject, body, channel)`; resolve by the recipient's locale, fallback to `ar`.
3. Deliver in-app (row in `platform.notifications`) and email (via a port; adapter in `Integrations`).
4. Preferences and quiet hours per user; `approval` and `security` categories bypass quiet hours `[spec]`.
5. Endpoints `[rec]`: `GET /api/v1/platform/notifications` (mine, keyset), `POST /notifications/{id}/read`, `GET/PUT /notifications/preferences`.
6. Delivery runs in a Worker job with retry (PLT-204).

**Security:** **escape all params** in templates (no HTML injection into emails or in-app); producers never pass raw text; recipients are resolved within the tenant only (a user id from another tenant is dropped and logged); email headers built from validated addresses only (no CRLF); no PII beyond what the template needs; unsubscribe/preference is respected except for security/approval.
**Races:** `dedupeKey` unique per `(tenant, user, dedupe_key)` prevents duplicates when an event is redelivered; `read` is idempotent.
**Scale:** fan-out in batches; index `(tenant_id, user_id, created_at desc)`; mail provider failures retry with backoff and never block the business transaction.
**Tests:** template resolution and `ar` fallback; HTML in params is escaped; quiet hours with/without bypass; dedupe on redelivery; cross-tenant recipient dropped; only my notifications visible (E4); preferences validation; email failure retried and dead-lettered.
**Traps:** sending email inside the business transaction; building the message with string concatenation; leaking record details in the email body for a user without access.

---

## PLT-210 — Excel import framework (Medium risk)

**Steps**
1. Port `IImportHandler` (modules register one per import type: `Parse(row)`, `Validate(row, ctx)`, `Commit(rows, ctx)`).
2. `POST /api/v1/platform/imports` (multipart or a `fileId` from PLT-207) ⇒ creates `platform.import_batches` (`status='uploaded'`) ⇒ parse job ⇒ `previewed`.
3. `GET /imports/{id}/preview?cursor=` ⇒ rows with **errors per row and column in Arabic and English**; counts of valid/invalid/warnings.
4. `POST /imports/{id}/commit` ⇒ conditional transition `previewed → committing` ⇒ run the handler's commit in chunks of 500-1,000 rows inside a transaction (all-or-nothing, or "skip errors" if the import type allows) ⇒ `committed`. Records get `source='import'`, `import_batch_id`.
5. Each import type declares max rows (default 20,000) and required columns.

**Security (S15):** only the batch owner (same tenant) can preview/commit; cap file size and **uncompressed** size (xlsx is a zip: reject ratios or sizes beyond limits); never evaluate formulas, read cached values or treat cells as text/number; reject macro-enabled files (`.xlsm`); strip control chars; header-name allow-list; escape on any later **export** (cells beginning with `= + - @` are prefixed) to avoid formula injection; use a parser library approved by the architect `[verify licence]`.
**Races:** `committing` transition is conditional; committing twice ⇒ 409; a batch edited after preview is not possible (rows are immutable after `previewed`).
**Scale:** stream rows; store parsed rows in a staging table `platform.import_rows(batch_id, row_no, data jsonb, errors jsonb)`; chunked commit; 5,000-row preview < 10 s `[spec]`; delete staging rows after N days.
**Tests:** valid file end to end; row/column errors reported; commit atomic (one bad row ⇒ nothing, in all-or-nothing mode); skip-errors mode; double commit ⇒ 409; other tenant/user ⇒ 404; zip-bomb sample rejected; `.xlsm` rejected; formula cell is not evaluated; 5,000-row timing; Arabic headers and digits handled.
**Traps:** parsing in the request thread; loading the whole workbook in memory; trusting the file extension; committing from the client's resubmitted rows instead of the staged ones.

---

## PLT-211 — CI/CD (Medium risk)

**Steps**
1. Pipeline stages: restore/build (`-warnaserror`) → unit → integration (Testcontainers) → architecture → contract (event schemas + OpenAPI diff) → tenant isolation → migrations (empty DB, and from the previous release tag) → security scans → publish → deploy to dev.
2. Generate OpenAPI at build, commit the snapshot, **fail** on a breaking diff unless the PR carries a labeled approval.
3. Security scans: `dotnet list package --vulnerable --include-transitive`, secret scanning, container image scan `[rec]`.
4. Environments: dev auto-deploy on `main`; promotion by tag; secrets from the platform secret store only.
5. Database roles: **migrator** (owner, DDL) used only by the deploy step; **app** (DML only, no BYPASSRLS) used by Api/Worker.
6. Caching of NuGet and test images to keep the fast set under about 5 minutes.
7. **Audit support:** expose the OpenAPI document at a stable path and provide a **test-only token issuer** (for example `POST /test/tokens`, or a signed-JWT helper script) that can mint tokens for tenant A with all permissions, tenant A with none, and tenant B with all. It must be compiled out or hard-disabled outside Development/Test (a startup check that fails if it is enabled in production). Document how in `audit/README.md` → Setup. Set `AUDIT_CMD` in `scripts/d2/d2.conf` only if the audit can run in the agent's environment.

**Security:** least-privilege deploy credentials; protected `main` (required checks, review); no secrets in logs (mask); artifacts signed or at least checksummed `[rec]`.
**Races:** deploy and migrate run once per release (concurrency group); migrations are backward-compatible so old and new instances can overlap.
**Scale:** parallelize test projects; Testcontainers reuse; split slow tests with the `Slow` trait.
**Tests (meta):** on a throwaway branch break each gate once (architecture rule, event schema, OpenAPI breaking change, vulnerable package, failing migration from previous release) and confirm the pipeline fails each time; record it in the PR.
**Traps:** running tests with the owner role; allowing "continue on error" on security steps; migrations that lock a big table.
