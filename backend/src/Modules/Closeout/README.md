# Closeout module

- Module code: `closeout`
- Owner / backup: see `.github/CODEOWNERS` (fill in)
- Spec: add the Notion link and update `docs/modules/README.md`
- Events published / consumed: see spec 00 section 7
- Tests: `backend/tests/Modules/Closeout`

Standard shape:

```
Closeout.Contracts/        public: Dtos, Queries, Events (+ JSON schemas), Permissions, ErrorCodes
Closeout.Core/             Domain, Application (feature folders + Ports), Integration (Inbound, Outbound, Acl)
Closeout.Infrastructure/   Persistence (own schema, DbContext, migrations), ReadModels, Jobs, Adapters
Closeout.Api/              HTTP endpoints only, feature folders mirror Core/Application
```

Project files and code are not added yet.
