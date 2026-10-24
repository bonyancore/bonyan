# Intelligence module

- Module code: `intelligence`
- Owner / backup: see `.github/CODEOWNERS` (fill in)
- Spec: add the Notion link and update `docs/modules/README.md`
- Events published / consumed: see spec 00 section 7
- Tests: `backend/tests/Modules/Intelligence`

Standard shape:

```
Intelligence.Contracts/        public: Dtos, Queries, Events (+ JSON schemas), Permissions, ErrorCodes
Intelligence.Core/             Domain, Application (feature folders + Ports), Integration (Inbound, Outbound, Acl)
Intelligence.Infrastructure/   Persistence (own schema, DbContext, migrations), ReadModels, Jobs, Adapters
Intelligence.Api/              HTTP endpoints only, feature folders mirror Core/Application
```

Project files and code are not added yet.
