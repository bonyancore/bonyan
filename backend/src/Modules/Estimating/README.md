# Estimating module

- Module code: `estimating`
- Owner / backup: see `.github/CODEOWNERS` (fill in)
- Spec: add the Notion link and update `docs/modules/README.md`
- Events published / consumed: see spec 00 section 7
- Tests: `backend/tests/Modules/Estimating`

Standard shape:

```
Estimating.Contracts/        public: Dtos, Queries, Events (+ JSON schemas), Permissions, ErrorCodes
Estimating.Core/             Domain, Application (feature folders + Ports), Integration (Inbound, Outbound, Acl)
Estimating.Infrastructure/   Persistence (own schema, DbContext, migrations), ReadModels, Jobs, Adapters
Estimating.Api/              HTTP endpoints only, feature folders mirror Core/Application
```

Project files and code are not added yet.
