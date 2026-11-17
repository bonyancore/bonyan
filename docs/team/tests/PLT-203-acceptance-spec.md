# PLT-203 Acceptance Specification — Background Jobs & Worker Host

**Version:** 1.0.0  
**Status:** Implemented & Verified  
**Related Tickets:** PLT-203, PLT-202, PLT-204  
**Architectural Decisions:** ADR-002 (In-Process Scheduler & Advisory Locks), RFC 0001 (Cross-Tenant Dispatcher Role)

---

## 1. Conformance Scenarios

### 1.1 Job Catalog & Registration

- **J01: Extensibility without Runner Modification:** Adding a new background job requires only implementing `IJob` and registering it in DI; `JobRunner` executes any registered `IJob` from `JobCatalog`.
- **J02: Module Job Enablement Filter:** Jobs whose owning module is disabled via configuration (`Modules:<Module> = false`) are skipped by `JobCatalog`.

### 1.2 Cron Schedule Parsing (ADR-002)

- **J03: Strict Space-Separated Syntax:** Format accepted is `M H * * *` (minute, hour, wildcard day-of-month, wildcard month, wildcard day-of-week).
- **J04: Time-Zone Evaluation:** Schedule evaluation uses the tenant's configured IANA time zone (e.g. `Africa/Cairo`, `Asia/Riyadh`).
- **J05: Rejection of Advanced Cron Syntax:** Step values (`*/5`), ranges (`1-5`), lists (`1,2`), or specific day-of-week throw `NotSupportedException` fail-closed to maintain deterministic in-process evaluation.

### 1.3 Concurrency Control & Advisory Locks

- **J06: PostgreSQL Advisory Locks:** `PostgresJobLock` uses `pg_try_advisory_lock(hashtext(job || tenant))` for transaction/lease duration.
- **J07: Mutual Exclusion:** Two workers cannot run the same job for the same tenant concurrently. Second attempt returns `null` immediately.
- **J08: Automatic Release on Failure:** Lease disposal calls `pg_advisory_unlock` ensuring lock release even on job crash or cancellation.

### 1.4 Run Persistence & Idempotency

- **J09: State Tracking in `platform.job_runs`:** Runs record `id`, `tenant_id`, `job`, `run_key`, `trigger`, `started_at`, `finished_at`, `status` (`running`, `succeeded`, `failed`, `timed_out`, `skipped`), `attempts`, `error_code`.
- **J10: Idempotency Key Guard (BR-PLT-140):** Unique constraint `uq_job_runs_tenant_job_run_key` on `(tenant_id, job, run_key)` prevents double execution for the same scheduled window.
- **J11: Tenant RLS Scoping:** All operations on `platform.job_runs` set `app.tenant_id`, ensuring tenant isolation under forced RLS.

### 1.5 Timeout & Cancellation

- **J12: Cancellation Propagation:** `CancellationToken` is passed to `IJob.RunAsync`. Timeout or shutdown cancels the token.
- **J13: No False Success:** A canceled or timed-out job run is recorded as `failed` or `timed_out`, never `succeeded`.

---

## 2. Test Evidence

| Scenario                               | Test Location                                                                         |  Result  |
| :------------------------------------- | :------------------------------------------------------------------------------------ | :------: |
| Run Persistence (Insert/Update/Get)    | `JobRunnerPostgresTests.PostgresJobRunStore_Insert_Update_Get_RoundTrip`              | **PASS** |
| Run Key Uniqueness                     | `JobRunnerPostgresTests.PostgresJobRunStore_DuplicateRunKey_ReturnsFalse`             | **PASS** |
| Advisory Lock Concurrency              | `JobRunnerPostgresTests.PostgresJobLock_AdvisoryLock_PreventsConcurrentRun`           | **PASS** |
| Tenant Isolation on Job Runs           | `JobRunnerPostgresTests.PostgresJobRunStore_TenantIsolation_CannotReadOtherTenantRun` | **PASS** |
| In-Memory Runner Unit Tests (24 tests) | `Platform.Domain.Tests.Jobs.*`                                                        | **PASS** |
