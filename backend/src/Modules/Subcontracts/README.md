# Subcontracts module

- Module code: `subcontracts`
- Owner / backup: see `.github/CODEOWNERS` (fill in)
- Spec: add the Notion link and update `docs/modules/README.md`
- Events published / consumed: see spec 00 section 7
- Tests: `backend/tests/Modules/Subcontracts`

Standard shape:

```
Subcontracts.Contracts/        public: Dtos, Queries, Events (+ JSON schemas), Permissions, ErrorCodes
Subcontracts.Core/             Domain, Application (feature folders + Ports), Integration (Inbound, Outbound, Acl)
Subcontracts.Infrastructure/   Persistence (own schema, DbContext, migrations), ReadModels, Jobs, Adapters
Subcontracts.Api/              HTTP endpoints only, feature folders mirror Core/Application
```

Project files and code are not added yet.
