# TemplateModule module

- Module code: `templatemodule`
- Owner / backup: see `.github/CODEOWNERS`
- Spec: see Notion specs
- Events published / consumed: see spec 00 section 7
- Tests: `backend/tests/Modules/TemplateModule`

Standard shape:

```
TemplateModule.Contracts/        public: Dtos, Queries, Events (+ JSON schemas), Permissions, ErrorCodes
TemplateModule.Core/             Domain, Application (feature folders + Ports), Integration (Inbound, Outbound, Acl)
TemplateModule.Infrastructure/   Persistence (own schema, DbContext, migrations), ReadModels, Jobs, Adapters
TemplateModule.Api/              HTTP endpoints only, feature folders mirror Core/Application
TemplateModule.Module/           Entry point implementing IModule
```

