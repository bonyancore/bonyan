# ADR-002: Hand-written job runner (no Hangfire)

**Status:** Accepted  
**Date:** 2026-10-30  
**Ticket:** PLT-203

## Context

PLT-203 requires a Worker host job runner with queues, per-tenant enablement, timeouts, and `platform.job_runs`. Brief/recipe allow Hangfire (PostgreSQL storage) or a small hand-written runner. Runbook §6 default #3: use the skeleton's library if any; otherwise the minimal hand-written runner and **add no new package**.

## Decision

Use a **hand-written** `JobRunner` + hosted loop in `Construction.Worker` / `Platform.Infrastructure`. No Hangfire (or other job-framework) package.

## Consequences

- Job scheduling is in-process (cron evaluated per tenant timezone); horizontal scale uses advisory locks so two workers do not overlap the same `(job, tenant)`.
- Outbox dispatch runs as a Worker-hosted loop alongside jobs (PLT-202 hand-off).
- Revisit Hangfire only if multi-node scheduling/ops needs outgrow this runner (new ADR + package approval).
