# Mobile

React Native site app for users on weak connections.

Planned folders (empty for now):
- `src/app/` screens and navigation
- `src/components/` shared UI
- `src/features/` one folder per backend module
- `src/lib/` API client (generated from `contracts/openapi/v1.json`), auth, secure storage
- `src/offline-queue/` queued writes, retried with the same `Idempotency-Key`
- `src/i18n/` Arabic-first, RTL, loads `contracts/messages/*`
- `assets/`, `tests/`

Backend rules that affect the client:
- Auth: JWT (15 min) plus rotating refresh token in secure storage
- `Idempotency-Key` is mandatory on every creating POST from mobile
- Writes must be safe to queue and replay (see spec 15, Portals and Mobile Sync)
- E2E flows use Maestro (`backend/tests/E2E`)

No packages installed yet. Initialise the project here when the team agrees on tooling.
