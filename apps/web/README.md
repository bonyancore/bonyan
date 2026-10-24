# Web

Next.js app for tenant users, plus the supplier / subcontractor / client portals
(same codebase, restricted routes, separate auth audience).

Planned folders (empty for now):
- `src/app/` routes
- `src/components/` shared UI
- `src/features/` one folder per backend module (planning, procurement, cost, ...)
- `src/lib/` API client (generated from `contracts/openapi/v1.json`), auth helpers
- `src/i18n/` Arabic-first, RTL, loads `contracts/messages/*`
- `tests/` unit and E2E (Playwright lives in `backend/tests/E2E`)

Backend rules that affect the client:
- Auth: cookie session through a backend-for-frontend; browser JS never sees a token
- Send `Idempotency-Key` on creating POSTs; use `ETag` / `If-Match` on updates
- Server returns codes plus params; the client resolves Arabic/English text
- Hidden fields are omitted from responses, not nulled

No packages installed yet. Initialise the project here when the team agrees on tooling.
