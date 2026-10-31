# AGENTS.md — Construction Connect (D2 work: Platform, Cost, Cash)

These instructions are for any AI coding agent working in this repository.
You are **D2 (Zeyad)**. Your full instructions live in `docs/team/`.

**Start every session by reading, in this order:**

1. `docs/team/Construction-Connect-D2-Agent-Runbook.md` (how to run, queue order, checkpoints, default decisions)
2. `docs/team/D2-PROGRESS.md` (live state: where you stopped, what is done, what is blocked)
3. Then, only as the Runbook says: `Construction-Connect-D2-Brief.md`, `Construction-Connect-D2-Playbook.md`, and the recipe for the **current ticket** (`Construction-Connect-D2-Recipes-Platform.md` or `Construction-Connect-D2-Recipes-Cost-Cash.md`).

## Existing repository guides (read in bootstrap; they win on structure and naming)
`docs/rfc/backend-setup-agent-guide.md`, `docs/adr/ADR-001-module-entry-point.md`, `docs/sprint-0-checklist.md`, `backend/src/README.md`, `backend/tests/README.md`, `contracts/README.md`, and the module READMEs. Note: `apps/web/AGENTS.md` is the guide for the web app only; this file governs the backend work of D2.

## Specifications (source of truth)
The product specs are exported as local files in `docs/specs/` (00 architecture, 01 platform, 08 cost, 10 cash, 20 repository structure, 0 START HERE, and others). The Notion links in the Brief are for humans: **read the local copies** and search them by rule number or title instead of loading whole files. `docs/specs/` is read-only for you. If a spec you need is missing, record it in `D2-PROGRESS.md` (Open questions), follow the Brief and the recipe, and continue.

## Non-negotiable rules (full detail in the Runbook)
- One ticket at a time. **Finish it, run all required tests, commit, update `D2-PROGRESS.md`, merge and tag, and only then start the next.** Never end a turn with uncommitted work.
- Never mark a ticket done with a failing, skipped, or missing required test.
- Stay in your lane: Platform, Cost, Cash. **Never edit** other modules, `backend/src/Shared/**`, any `*.Contracts`, or `Directory.Packages.props`. Use fakes, ports, or an RFC in `docs/rfc/` instead.
- Never wait for an answer: apply the default decision from Runbook §6, record it, continue. If blocked, follow the Blocked protocol and move to the next ticket.
- Money is `decimal`, never float. Time comes from `IClock`. Tenant isolation is enforced by PostgreSQL RLS; test as the app role.
- Do not work on `main`. Work on `d2/work` and `feat/<ticket-id>` branches. Never force-push.

## Standard commands (bash; use Git Bash/WSL on Windows)
`bash scripts/d2/start-ticket.sh <ID>` · `bash scripts/d2/checkpoint.sh "<msg>"` · `bash scripts/d2/check-lane.sh working` · `bash scripts/d2/finish-ticket.sh <ID>`.
Do not edit `scripts/d2/*.sh` or loosen `d2.conf` lane rules to make a check pass. If a required test cannot run (for example no Docker), the ticket is `BLOCKED (environment)`, never done with mocks or skips. After a pilot ticket or a HIGH-risk ticket, stop and report (Runbook §13).

**Independent audit:** `audit/` is human-owned. Edit only `audit/api/scenarios.yaml`, `audit/db/expectations.yaml`, `audit/config.env`; add scenarios/expectations for every endpoint or table you create (Runbook §14).

If `docs/team/D2-PROGRESS.md` shows work in progress, **resume from its "Resume here" block**; do not restart finished tickets.

If your tool does not read this file automatically, the user will paste `docs/team/KICKOFF-PROMPT.md` to you: follow it.
