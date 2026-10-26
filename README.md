# Bonyan

Arabic-first construction operating system for contractors (Egypt first, Saudi later).

This is the **starter repository**: folder structure and conventions only.
There is no implementation and no packages yet. Each team fills in its own area.

## Repository layout

```
Bonyan/
├── apps/
│   ├── web/          Next.js: tenant web app + portals (same codebase, restricted routes)
│   └── mobile/       React Native: site app with offline queue
├── backend/
│   ├── src/          .NET 10 modular monolith (Host, Shared, Modules, Integrations)
│   ├── services/     Python intelligence service (FastAPI)
│   ├── tests/        Architecture, contract, tenant isolation, golden project, E2E, load
│   └── tools/        Module template and scripts
├── contracts/        What other teams consume: OpenAPI, event schemas, messages, AI schemas
├── deploy/           Dockerfiles, environment settings (no secrets), infrastructure as code
├── docs/             ADRs, RFCs, runbooks, module index
└── .github/          CODEOWNERS, PR template, workflows
```

## Who works where

| Area | Folder |
|------|--------|
| Backend (.NET) | `backend/src`, `backend/tests` |
| Web | `apps/web` |
| Mobile | `apps/mobile` |
| AI / Intelligence | `backend/services/intelligence`, `contracts/intelligence` |
| DevOps | `deploy`, `.github`, `backend/tools` |

Front-end and AI engineers code against `contracts/` and never against backend internals.

## Source of truth (Notion)

- Construction Connect specs: `00` Backend architecture and conventions, `20` Repository structure and module integration, `17` Control plane and entitlements, `14` Intelligence service
- Backend Engineering Handbook: `BE-00` to `BE-12`

If this repo and the specs disagree, raise it in review and fix one of them.
See `docs/modules/README.md` for the module index.

## Golden rules (from the Backend Engineering Handbook)

1. One database, one schema per module, no cross-schema foreign keys.
2. Modules talk only through `.Contracts` (sync, read-only) or events (async).
3. Tenant isolation ends in PostgreSQL row-level security.
4. Every endpoint, job and consumer declares its module, permission and project scope.
5. Money and quantities are `numeric(18,4)`. Ledgers are append-only. Approved records are frozen.
6. Agents suggest, humans confirm.
7. If it is not tested, observed and documented, it is not done.

## Deviation from spec 20

Spec 20 puts `src/`, `tests/`, `tools/` and `services/` at the repo root.
Because this repo also holds web and mobile, they live under `backend/`.
`contracts/`, `deploy/`, `docs/` and `.github/` stay at the root. Paths in
`.github/CODEOWNERS` already reflect this. Update spec 20 or this layout in an ADR.

## Getting started

Each area has its own README with what to add first. Nothing builds yet.
Sprint 0 goals are listed in `docs/sprint-0-checklist.md`.
