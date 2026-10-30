# Backend source (.NET 10, modular monolith)

- `Host/`: composition roots. `Construction.Api` (HTTP) and `Construction.Worker` (jobs, outbox, consumers). Each will hold an explicit `AppModules.cs`
- `Shared/`: technical building blocks, no business rules. Kernel, Messaging, Persistence, Web, Testing
- `Modules/`: business modules, all in the same shape (see any module README)
- `Integrations/`: adapters to external systems behind ports

Dependency rules (enforced later by `tests/Architecture.Tests`):
- `X.Contracts` is the only project other modules may reference
- `X.Core` has no EF Core, ASP.NET, Hangfire or vendor SDKs
- `Shared/*` never references `Modules/*`
- No module reads another module's tables

Proposed modules `Equipment` and `Stores` are not created yet (specs 18 and 19 are unwritten).
Create them with the module template when the specs are approved.
