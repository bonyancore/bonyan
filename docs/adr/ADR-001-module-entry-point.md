# ADR-001: Module Entry-Point Registration and Composition

**Status:** Accepted by owner, pending architect review  
**Date:** 2026-10-29  

## Context

The architecture forbids `X.Api` from referencing `X.Infrastructure` and the reverse. Yet the composition root (the Host) must wire both the module's services/infrastructure and its API endpoints. There was previously no project allowed to reference both.

Furthermore, Spec 20 section 6 defines `IModule` with:
- `AddServices(IServiceCollection services, IConfiguration config)`
- `MapEndpoints(IEndpointRouteBuilder app)`
- `AddConsumers(IConsumerRegistry consumers)`
- `AddJobs(IJobRegistry jobs)`
- `Permissions` and `Migrations` metadata collections

## Options Considered

1. **Fifth tiny `X.Module` project** – `X.Module` references `X.Infrastructure`, `X.Api`, `X.Core`, `X.Contracts`, and the shared module abstraction `Construction.Modules`. Only the Host references `X.Module`. Clean, preserves inward dependencies, and maintains modular encapsulation.
2. **Registration lives in the Host** – The Host explicitly calls each module's infrastructure and API registrations. Violates single-line module registration and becomes unmaintainable across 14+ modules.
3. **Relax the `X.Api` ↔ `X.Infrastructure` rule** – Allow direct cross-referencing between Api and Infrastructure. Breaks clean architectural boundaries.

## Decision

**Option 1 is adopted.** Each module has a fifth project, `X.Module` (e.g., `Construction.Platform.Module`), which serves as the module's composition root and entry point:
- Implements `IModule` from `Construction.Modules`.
- The Host (`Construction.Api` and `Construction.Worker`) references `X.Module` as the sole entry point per module.
- Host registration is reduced to a single line in `AppModules.cs`: `services.AddModule<PlatformModule>(config)`.

### Known Deviations from Spec 20 (Intentional)
1. **Fifth Project (`X.Module`):** Spec 20 originally outlined four projects per module. Option 1 introduces `X.Module` as the fifth project to bridge Api and Infrastructure cleanly without violating project dependency directions.
2. **Deferred Consumer/Job Registrations:** `AddConsumers` and `AddJobs` are excluded from `IModule` in PLT-102 and deferred to PLT-201 and PLT-203 (Zeyad's scope).
3. **Worker Framework Reference:** Referencing `X.Module` brings the `Microsoft.AspNetCore.App` framework reference into `Construction.Worker` via transitive dependencies. This is accepted for unified module composition.

## Dependency Rules for Architecture Enforcement (PLT-112)

The architecture tests in PLT-112 will enforce:
1. `Construction.Modules` may only reference `Construction.Kernel` and the `Microsoft.AspNetCore.App` framework reference.
2. `X.Module` may reference `X.Api`, `X.Infrastructure`, `X.Core`, `X.Contracts`, and `Construction.Modules`.
3. `X.Module` must never be referenced by any business module (`Core`, `Infrastructure`, `Api`, or `Contracts`); it is referenced only by the Host projects (`Construction.Api`, `Construction.Worker`).
4. `X.Api` and `X.Infrastructure` still must never reference each other directly.
