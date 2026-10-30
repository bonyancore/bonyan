# Contracts

Where the backend meets everyone else. Front-end and AI engineers code against these files.

| Folder | Content | Produced by |
|--------|---------|-------------|
| `openapi/` | `v1.json` | Generated at backend build, committed, diffed in CI. Breaking change needs `/v2` |
| `events/` | Integration event JSON schemas | Copied from each module's `.Contracts/Events` by `backend/tools/scripts/copy-event-schemas` |
| `messages/` | `messages.ar.json`, `messages.en.json` | Merged from per-module catalogues at build |
| `intelligence/` | Request/response schemas for the Python service | Owned by the architect; both sides generate models |

Do not edit generated files by hand.
