# Docs module

The Docs module is registered through `Docs.Module` and exposes routes under `/api/v1/docs`. It stores generic
record links in its own `docs` schema, so a document can point to a purchase order, supplier, invoice, or another
record without a foreign key into that module. Every document and version is tenant-scoped and protected by RLS.

## Implemented work

- **DOC-401:** document metadata linked by record type and record ID; a file-service port confirms file IDs before
  metadata is saved.
- **DOC-402:** numbered document versions with `Draft`, `Approved`, and `Superseded` states. Approving a new
  version supersedes the previous approved one.
- EF Core persistence, audit fields, concurrency protection on document records, and tenant-scoped migrations.

Development settings enable a temporary PLT-207 file metadata adapter. It accepts non-empty file IDs for local
workflow development; replace it with the real file service before relying on file existence checks in production.

The domain and persistence test projects cover version transitions, EF mappings, and tenant query filters. The
PostgreSQL RLS migration itself needs Docker-backed integration verification.
