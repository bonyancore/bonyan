# PLT-204 → PLT-209 — Branch & PR Plan

The working copy has **no `.git`**, no remote and no `gh` CLI, so no branch or PR exists yet.
This file is the executable split: exact file lists, dependency map, merge order and the commands to
create the six branches and six PRs on a real clone.

Full verification evidence: `docs/team/PLT-204-209-QA-REPORT.md` (477 passed / 0 failed / 48 skipped,
Release build clean). Implementation report: `docs/team/PLT-204-209-EXECUTION-REPORT.md`.

---

## 1. Dependency map and merge order

```
main
 └── PLT-204  retries / DLQ / replay      foundation: error types, platform. catalogue, audit log, tenant GUC
      └── PLT-205  idempotency            needs 204 (error types, Web pipeline)
           ├── PLT-206  number series      needs 205 (Idempotency-Key on POST/PATCH)
           ├── PLT-207  files              needs 205
           ├── PLT-208  approvals          needs 205 + 204 catalogue
           └── PLT-209  notifications      needs 205 + 204 catalogue
```

- **PLT-204 is the only branch based directly on `main`.**
- **PLT-205 is stacked on PLT-204**; its PR targets `feat/plt-204-retries-dlq-replay` and is retargeted
  to `main` after 204 merges.
- **PLT-206…209 are stacked on PLT-205** (independent of each other), so their PRs target
  `feat/plt-205-platform-services` and are retargeted to `main` after 205 merges.
- Merge order: **204 → 205 → 206 → 207 → 208 → 209**. No PR is auto-merged; nothing is pushed to `main`.

Why stacked rather than six parallel branches from `main`: the three registration files
(`PlatformApiRegistration.cs`, `PlatformInfrastructureRegistration.cs`, `PlatformModule.cs`) and
`Program.cs` receive lines from five tickets. A single stack keeps those shared files conflict-free and
keeps each branch buildable, which a parallel split from `main` would not.

---

## 2. Exact file lists

Shared files are assigned to the branch that first needs them and reach later branches through stacking.
Files marked *(hunk)* contain edits for several tickets: stage them with `git add -p` so each branch adds
only its own lines.

### feat/plt-204-retries-dlq-replay (base: `main`)

```
backend/src/Shared/Construction.Kernel/Error.cs
backend/src/Shared/Construction.Web/Errors/ErrorExtensions.cs
backend/src/Shared/Construction.Persistence/TenantTransactionInterceptor.cs
backend/src/Modules/Platform/Platform.Contracts/PlatformErrorCodes.cs
backend/src/Modules/Platform/Platform.Core/Application/Audit/IAuditLog.cs
backend/src/Modules/Platform/Platform.Infrastructure/Audit/PostgresAuditLog.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121093000_AddAuditLog.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/Outbox/DeadLetterSingleConsumerGuardTests.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/PlatformErrorCodesTests.cs
backend/tests/Shared/Construction.Web.Tests/Errors/ErrorExtensionsTests.cs          (hunk)
docs/team/PLT-204-209-EXECUTION-REPORT.md
docs/team/PLT-204-209-BRANCH-PLAN.md
docs/team/D2-PROGRESS.md                                                             (hunk)
```

Untouched: the pre-existing outbox dispatcher, retry ladder and dead-letter service (already on `main`).

### feat/plt-205-platform-services (base: `feat/plt-204-retries-dlq-replay`)

```
backend/src/Shared/Construction.Web/Idempotency/IIdempotencyStore.cs
backend/src/Shared/Construction.Web/Idempotency/RequestHasher.cs
backend/src/Shared/Construction.Web/Idempotency/IdempotencyModels.cs
backend/src/Shared/Construction.Web/Idempotency/IdempotencyOptions.cs
backend/src/Shared/Construction.Web/Idempotency/IdempotencyMetadata.cs
backend/src/Shared/Construction.Web/Idempotency/IdempotencyDecisionEngine.cs
backend/src/Shared/Construction.Web/Idempotency/InMemoryIdempotencyStore.cs
backend/src/Shared/Construction.Web/Idempotency/IdempotencyMiddleware.cs
backend/src/Shared/Construction.Web/WebServiceCollectionExtensions.cs               (hunk)
backend/src/Modules/Platform/Platform.Infrastructure/Idempotency/PostgresIdempotencyStore.cs
backend/src/Modules/Platform/Platform.Infrastructure/Jobs/Runner/IdempotencyPurgeJob.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121090000_AddIdempotencyKeys.cs
backend/src/Host/Construction.Api/Program.cs                                        (hunk: middleware + dev store)
backend/tests/Shared/Construction.Web.Tests/Idempotency/RequestHasherTests.cs
backend/tests/Shared/Construction.Web.Tests/Idempotency/IdempotencyDecisionEngineTests.cs
backend/tests/Shared/Construction.Web.Tests/Idempotency/InMemoryIdempotencyStoreTests.cs
```

### feat/plt-206-cost-cash (base: `feat/plt-205-platform-services`)

```
backend/src/Modules/Platform/Platform.Core/Domain/NumberSeries/NumberSeriesRules.cs
backend/src/Modules/Platform/Platform.Core/Application/NumberSeries/INumberAllocator.cs
backend/src/Modules/Platform/Platform.Core/Application/NumberSeries/INumberSeriesStore.cs
backend/src/Modules/Platform/Platform.Contracts/NumberSeriesContracts.cs
backend/src/Modules/Platform/Platform.Api/NumberSeriesEndpoints.cs
backend/src/Modules/Platform/Platform.Infrastructure/NumberSeries/PostgresNumberAllocator.cs
backend/src/Modules/Platform/Platform.Infrastructure/NumberSeries/PostgresNumberSeriesStore.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121100000_AddNumberSeries.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/NumberSeries/NumberSeriesRulesTests.cs
backend/src/Modules/Platform/Platform.Api/PlatformApiRegistration.cs                 (hunk)
backend/src/Modules/Platform/Platform.Infrastructure/PlatformInfrastructureRegistration.cs (hunk)
backend/src/Modules/Platform/Platform.Module/PlatformModule.cs                      (hunk: permissions)
```

> Ticket title vs plan: the executive plan defines PLT-206 as the number-series slice. No Cost/Cash
> monetary feature exists in the plan; the `Cash` module is an empty scaffold. This mismatch is a
> business decision, reported rather than invented.

### feat/plt-207-file-services (base: `feat/plt-205-platform-services`)

```
backend/src/Modules/Platform/Platform.Core/Domain/Files/FilePolicy.cs
backend/src/Modules/Platform/Platform.Core/Application/Files/FilePorts.cs
backend/src/Modules/Platform/Platform.Core/Application/Files/IFileService.cs
backend/src/Modules/Platform/Platform.Contracts/FileContracts.cs
backend/src/Modules/Platform/Platform.Api/FileEndpoints.cs
backend/src/Modules/Platform/Platform.Infrastructure/Files/PostgresFileService.cs
backend/src/Modules/Platform/Platform.Infrastructure/Files/InMemoryObjectStorage.cs
backend/src/Modules/Platform/Platform.Infrastructure/Files/UnavailableAdapters.cs
backend/src/Modules/Platform/Platform.Infrastructure/Files/FileAccessCheckers.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121110000_AddFiles.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/Files/FilePolicyTests.cs
backend/src/Modules/Platform/Platform.Api/PlatformApiRegistration.cs                 (hunk)
backend/src/Modules/Platform/Platform.Infrastructure/PlatformInfrastructureRegistration.cs (hunk)
backend/src/Modules/Platform/Platform.Module/PlatformModule.cs                      (hunk)
backend/src/Host/Construction.Api/Program.cs                                        (hunk: dev storage/access checker)
```

### feat/plt-208-approval-services (base: `feat/plt-205-platform-services`)

```
backend/src/Modules/Platform/Platform.Core/Domain/Approvals/ApprovalPolicyMatcher.cs
backend/src/Modules/Platform/Platform.Core/Application/Approvals/ApprovalPorts.cs
backend/src/Modules/Platform/Platform.Contracts/ApprovalContracts.cs
backend/src/Modules/Platform/Platform.Api/ApprovalEndpoints.cs
backend/src/Modules/Platform/Platform.Infrastructure/Approvals/PostgresApprovalService.cs
backend/src/Modules/Platform/Platform.Infrastructure/Approvals/FakeApproverDirectory.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121120000_AddApprovals.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/Approvals/ApprovalPolicyMatcherTests.cs
backend/src/Modules/Platform/Platform.Api/PlatformApiRegistration.cs                 (hunk)
backend/src/Modules/Platform/Platform.Infrastructure/PlatformInfrastructureRegistration.cs (hunk)
backend/src/Modules/Platform/Platform.Module/PlatformModule.cs                      (hunk)
backend/src/Host/Construction.Api/Program.cs                                        (hunk: dev approver directory)
```

### feat/plt-209-notifications (base: `feat/plt-205-platform-services`)

```
backend/src/Modules/Platform/Platform.Core/Domain/Notifications/NotificationRules.cs
backend/src/Modules/Platform/Platform.Core/Application/Notifications/NotificationPorts.cs
backend/src/Modules/Platform/Platform.Contracts/NotificationContracts.cs
backend/src/Modules/Platform/Platform.Api/NotificationEndpoints.cs
backend/src/Modules/Platform/Platform.Infrastructure/Notifications/PostgresNotificationService.cs
backend/src/Modules/Platform/Platform.Infrastructure/Notifications/EmailAdapters.cs
backend/src/Modules/Platform/Platform.Infrastructure/Jobs/Runner/NotificationDispatchJob.cs
backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121130000_AddNotifications.cs
backend/tests/Modules/Platform/Platform.Domain.Tests/Notifications/NotificationRulesTests.cs
backend/src/Modules/Platform/Platform.Api/PlatformApiRegistration.cs                 (hunk)
backend/src/Modules/Platform/Platform.Infrastructure/PlatformInfrastructureRegistration.cs (hunk)
backend/src/Modules/Platform/Platform.Module/PlatformModule.cs                      (hunk)
backend/src/Host/Construction.Api/Program.cs                                        (hunk: dev resolver)
docs/team/PLT-204-209-QA-REPORT.md
```

---

## 3. Commands for a real clone

```bash
git clone <repo-url> bonyan && cd bonyan
git switch main && git pull --ff-only

# copy this working copy over the clone (same layout)
#   robocopy "…\bonyan-main (1)\bonyan-main" "…\bonyan" /E /XD .git bin obj node_modules .vs

# ---- PLT-204 ----
git switch -c feat/plt-204-retries-dlq-replay main
git add -A -- backend/src/Shared/Construction.Kernel/Error.cs \
  backend/src/Shared/Construction.Web/Errors/ErrorExtensions.cs \
  backend/src/Shared/Construction.Persistence/TenantTransactionInterceptor.cs \
  backend/src/Modules/Platform/Platform.Contracts/PlatformErrorCodes.cs \
  backend/src/Modules/Platform/Platform.Core/Application/Audit \
  backend/src/Modules/Platform/Platform.Infrastructure/Audit \
  backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121093000_AddAuditLog.cs \
  backend/tests/Modules/Platform/Platform.Domain.Tests/Outbox/DeadLetterSingleConsumerGuardTests.cs \
  backend/tests/Modules/Platform/Platform.Domain.Tests/PlatformErrorCodesTests.cs \
  docs/team/PLT-204-209-EXECUTION-REPORT.md docs/team/PLT-204-209-BRANCH-PLAN.md
git add -p backend/tests/Shared/Construction.Web.Tests/Errors/ErrorExtensionsTests.cs docs/team/D2-PROGRESS.md
dotnet test backend/tests/Modules/Platform/Platform.Domain.Tests -c Release \
  --filter "FullyQualifiedName~DeadLetterSingleConsumerGuard|FullyQualifiedName~PlatformErrorCodes"
git commit -m "feat(platform): PLT-204 harden retries, dead letters and replay"
git push -u origin feat/plt-204-retries-dlq-replay

# ---- PLT-205 (stacked) ----
git switch -c feat/plt-205-platform-services feat/plt-204-retries-dlq-replay
git add -A -- backend/src/Shared/Construction.Web/Idempotency \
  backend/src/Modules/Platform/Platform.Infrastructure/Idempotency \
  backend/src/Modules/Platform/Platform.Infrastructure/Jobs/Runner/IdempotencyPurgeJob.cs \
  backend/src/Modules/Platform/Platform.Infrastructure/Persistence/Migrations/20261121090000_AddIdempotencyKeys.cs \
  backend/tests/Shared/Construction.Web.Tests/Idempotency
git add -p backend/src/Shared/Construction.Web/WebServiceCollectionExtensions.cs \
  backend/src/Host/Construction.Api/Program.cs docs/team/D2-PROGRESS.md
dotnet test backend/tests/Shared/Construction.Web.Tests -c Release --filter "FullyQualifiedName~Idempotency"
git commit -m "feat(platform): PLT-205 Idempotency-Key middleware and store"
git push -u origin feat/plt-205-platform-services

# ---- PLT-206 … PLT-209: same pattern, each branched from feat/plt-205-platform-services ----
#   git switch -c feat/plt-206-cost-cash feat/plt-205-platform-services
#   git switch -c feat/plt-207-file-services feat/plt-205-platform-services
#   git switch -c feat/plt-208-approval-services feat/plt-205-platform-services
#   git switch -c feat/plt-209-notifications feat/plt-205-platform-services
# Run each ticket's tests (see §4) before committing.
```

### Pull requests

```bash
gh pr create --base feat/plt-204-retries-dlq-replay --head feat/plt-205-platform-services \
  --title "PLT-205: Idempotency-Key middleware" \
  --body "Stacked on PLT-204. Retarget to main once PLT-204 merges."

gh pr create --base feat/plt-205-platform-services --head feat/plt-206-cost-cash        --title "PLT-206: number series"
gh pr create --base feat/plt-205-platform-services --head feat/plt-207-file-services     --title "PLT-207: file services"
gh pr create --base feat/plt-205-platform-services --head feat/plt-208-approval-services --title "PLT-208: approval services"
gh pr create --base feat/plt-205-platform-services --head feat/plt-209-notifications     --title "PLT-209: notifications"

# after PLT-204 merges:
gh pr edit <plt-205-pr> --base main        # merge it
gh pr edit <plt-206-pr> --base main        # then 207, 208, 209
```

Rules honoured: one PR per ticket; no automatic merges; no direct pushes to `main`; no force-push;
only the ticket's own files per branch.

---

## 4. Ticket-specific tests to run before each PR

```bash
# PLT-204
dotnet test backend/tests/Modules/Platform/Platform.Domain.Tests -c Release \
  --filter "FullyQualifiedName~DeadLetter|FullyQualifiedName~PlatformErrorCodes"

# PLT-205
dotnet test backend/tests/Shared/Construction.Web.Tests -c Release --filter "FullyQualifiedName~Idempotency"      # 20
dotnet test backend/tests/Modules/Platform/Platform.Domain.Tests -c Release --filter "FullyQualifiedName~PlatformErrorCodes"

# PLT-206
dotnet test backend/tests/Modules/Platform/Platform.Domain.Tests -c Release --filter "FullyQualifiedName~NumberSeriesRules"

# PLT-207
dotnet test backend/tests/Modules/Platform/Platform.Domain.Tests -c Release --filter "FullyQualifiedName~FilePolicy"

# PLT-208
dotnet test backend/tests/Modules/Platform/Platform.Domain.Tests -c Release --filter "FullyQualifiedName~ApprovalPolicyMatcher"

# PLT-209
dotnet test backend/tests/Modules/Platform/Platform.Domain.Tests -c Release --filter "FullyQualifiedName~NotificationRules"
```

Current evidence on the combined tree: 477 passed / 0 failed / 48 skipped (environment opt-outs),
Release build clean — see `docs/team/PLT-204-209-QA-REPORT.md` §2.
