# ADR-001: Module Entry-Point Registration Gap

**Status:** Open  
**Date:** 2026-10-29  

## Context

The architecture forbids `X.Api` from referencing `X.Infrastructure` and the reverse. Yet the composition root (the Host) must call both `AddPlatformInfrastructure(config)` and wire up `Platform.Api` endpoints. There is no project that is allowed to reference both.

## Options

1. **Fifth tiny `X.Module` project** – `Platform.Module` references both `Platform.Infrastructure` and `Platform.Api`; only the Host references `Platform.Module`. Clean but adds one project per module.
2. **Registration lives in the Host** – The Host explicitly calls each module's infrastructure and API registrations. Simple now, harder to scale across 14 modules.
3. **Relax the `X.Api` ↔ `X.Infrastructure` rule** – Allow a deliberate, one-directional reference (Infrastructure → Api forbidden, but Api → Infrastructure allowed, or vice versa). Changes the architecture constraint.

## Decision

**Deferred.** For the bootstrap, option 2 is used (explicit calls in `AppModules.cs`). The architect must decide before the second module is built.

## Consequences

- `AppModules.cs` in each Host lists registrations explicitly.  
- A follow-up task must implement the chosen pattern before adding the second module.
