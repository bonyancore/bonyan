# Reporting module

- Module code: `reporting`
- Owner / backup: see `.github/CODEOWNERS` (fill in)
- Spec: add the Notion link and update `docs/modules/README.md`
- Events published / consumed: see spec 00 section 7
- Tests: `backend/tests/Modules/Reporting`

Standard shape:

```
Reporting.Contracts/        public: Dtos, Queries, Events (+ JSON schemas), Permissions, ErrorCodes
Reporting.Core/             Domain, Application (feature folders + Ports), Integration (Inbound, Outbound, Acl)
Reporting.Infrastructure/   Persistence (own schema, DbContext, migrations), ReadModels, Jobs, Adapters
Reporting.Api/              HTTP endpoints only, feature folders mirror Core/Application
```

Project files and code are not added yet.
