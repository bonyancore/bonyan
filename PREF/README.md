# docs/specs — product specifications (read-only for agents)

Exported from the Notion library "Construction Connect" (database: https://app.notion.com/p/2764cdd2129683b5874481b6cf83a068).
**Read-only:** the agent never edits this folder (lane rule). Changes are made in Notion and re-exported by a human.
File naming: `NN-short-name.md`, where `NN` is the document number used by the Brief, the Runbook and the recipes (for example "spec 08").

## Included (converted from Notion, text verified identical to the source)
| File | Spec |
|---|---|
| `01-platform-foundation.md` | 01 Platform foundation |
| `02-core-project-spine-and-master-data.md` | 02 Core project spine and master data |
| `04-estimating-and-tendering.md` | 04 Estimating and tendering |
| `05-planning-and-site-execution.md` | 05 Planning and site execution |
| `06-procurement.md` | 06 Procurement |
| `07-automated-sourcing.md` | 07 Automated sourcing |
| `10-cash-flow.md` | 10 Cash flow |

## MUST be added by a human (not exported yet)
The D2 work depends on these. Export them from Notion (see below) and save them here with the same naming rule:
| Needed file | Why |
|---|---|
| `00-backend-architecture-and-conventions.md` | **Most important**: conventions every ticket follows (events, errors, tenancy, API rules) |
| `08-cost-control.md` | CST-201 … CST-206 |
| `20-repository-structure.md` | Repository structure and module integration map |
| `0-start-here.md` (or `00-start-here.md`) | Team plan and sprint order (D2's tickets) |
| `03-reporting.md` | Referenced by cost and cash rules |
| `09-contracts.md` | Payment applications and contract events that cash consumes (CSH-201) |

Also useful: B2 (business plan v2), S1 (positioning), 11-17, B3, A1, D1, 21.

## How to export from Notion
1. Open the "Construction Connect" database page.
2. `•••` (top right) → **Export** → Format **Markdown & CSV** → enable **Include subpages** → Export.
3. Unzip. Copy the files you need into `docs/specs/` and rename them to `NN-short-name.md` (remove the trailing hash Notion adds).
4. If a page has tables, check they still render as Markdown tables (Notion exports them as pipe tables).

## Rules for agents
- Search these files by rule number (for example `BR-CST-11`) or heading instead of loading whole files (they are large).
- If a spec you need is missing here, write it under "Open questions" in `docs/team/D2-PROGRESS.md` and follow the Brief and the recipe; do not guess numbers.
- Spec section numbers (`§`) in the team files may be off by one heading: search by title.
