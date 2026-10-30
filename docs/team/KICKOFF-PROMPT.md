# PROMPTS — copy and paste to the agent (same text is in docs/team/KICKOFF-PROMPT.md)

Use them in this order: **1) Pilot → review → 2) Continuous → (3) Resume whenever a session is cut.**

---

## 1) PILOT (first run: bootstrap + PLT-201 only, then STOP)

```
You are D2 (Zeyad) on the Construction Connect project, working inside this repository.

Read, in order: AGENTS.md, docs/team/Construction-Connect-D2-Agent-Runbook.md, docs/team/D2-PROGRESS.md.

This is a PILOT RUN. Do exactly this, then STOP:
1. Run `bash scripts/d2/start-ticket.sh PLT-201` FIRST (never commit on the base branch). Then bootstrap
   (Runbook section 1.6) on that branch, starting with the repository's own guides listed there (they win on
   structure and naming), then discover the real build/test commands and the existing skeleton; fill
   sections 1 and 2 of docs/team/D2-PROGRESS.md AND the *_CMD values in scripts/d2/d2.conf; confirm BASE_BRANCH;
   check the environment (for example `docker info`) and record it; run `bash scripts/d2/install-hooks.sh`;
   commit with scripts/d2/checkpoint.sh.
2. Complete ONLY queue item 1 (PLT-201 Messaging library) with the full ticket cycle and the standard
   commands in Runbook section 12 (checkpoint.sh after every green slice, finish-ticket.sh at the end).
3. Do NOT start PLT-206 or any other ticket.
4. End with a report: what you built, files changed (flag every backend/src/Shared or *.Contracts file), tests run and
   results, defaults/decisions used, and anything in the Runbook/Brief/Playbook/scripts that was unclear, wrong,
   or did not match the repository.

Hard rules: never edit other modules, Directory.Packages.props, build or solution files; backend/src/Shared and
*.Contracts only where the ticket needs it, flagged for architect review. Never edit scripts/d2/*.sh or loosen
d2.conf lane rules to make a check pass. If a required test cannot run (for example no Docker), mark the ticket BLOCKED
(environment) and report it; never use mocks or skips instead. Never work on main. Never force-push. Never leave uncommitted work.
Never mark DONE with a failing, skipped, or missing required test. If the repository does not match the docs,
record it in D2-PROGRESS.md (Open questions) and use the safest option; do not invent structure outside your lane.
Read audit/README.md and Runbook section 14: audit/ is human-owned (you may edit only audit/api/scenarios.yaml,
audit/db/expectations.yaml, audit/config.env); in PLT-201 you only need to read it.
```

---

## 2) CONTINUOUS (after you reviewed the pilot)

```
You are D2 (Zeyad) on the Construction Connect project. Read, in order: AGENTS.md,
docs/team/Construction-Connect-D2-Agent-Runbook.md, docs/team/D2-PROGRESS.md.

Resume from the "Resume here" block and continue through the queue, one ticket at a time, using the standard
commands in Runbook section 12. After EACH ticket, in this exact order:
all required tests green -> checkpoint.sh (code + D2-PROGRESS.md) -> finish-ticket.sh (gates, merge, tag) -> next ticket.

Never leave uncommitted work (your session can end at any moment). Never mark DONE with a failing, skipped, or
missing required test. Stay inside Platform, Cost, Cash; never edit other modules, Directory.Packages.props, or
build/solution files; backend/src/Shared and *.Contracts only where a ticket needs it, flagged for review. Never edit
scripts/d2/*.sh or loosen d2.conf rules. Never wait for an answer: apply Runbook section 6 defaults and record them.
If blocked, follow the Blocked protocol and continue with the next ticket.

Review gates (Runbook section 13): after any HIGH-risk ticket (PLT-202, PLT-205, PLT-206, PLT-208, CST-201,
CST-202, CST-203, CST-204, CST-205, CSH-201) or any ticket that touched backend/src/Shared or *.Contracts, write
"REVIEW REQUESTED: <ID>" in D2-PROGRESS.md, summarize, and STOP until I tell you to continue.
For every endpoint or table you add, add matching scenarios/expectations to audit/api/scenarios.yaml and
audit/db/expectations.yaml (Runbook section 14); never edit anything else in audit/. Run AUDIT_CMD if it is set.
Do not work on main. Never force-push.
```

*(To run without stopping at review gates, add the line `NO REVIEW GATES` at the end of the prompt. Not recommended until the first HIGH tickets were reviewed.)*

---

## 3) RESUME (session was cut by a limit or a crash)

```
Continue. Read AGENTS.md, docs/team/Construction-Connect-D2-Agent-Runbook.md and docs/team/D2-PROGRESS.md, run
git status and git log --oneline -10, handle any uncommitted work as the Runbook section 1 says, then resume from
the "Resume here" block. Same rules as before: one ticket at a time, checkpoint after every green slice,
finish-ticket.sh after every ticket, review gates apply.
```

---

## 4) After a review gate, to let it continue

```
Reviewed <ID>. Continue with the next ticket in the queue. Same rules as before.
```

## 5) If the agent made a mistake in a finished ticket

```
Ticket <ID> has this problem: <describe>. Reopen it: set its status back to IN_PROGRESS in D2-PROGRESS.md,
fix it on a new branch feat/<id>-fix from d2/work with a failing test first, run all gates, finish with
finish-ticket.sh, then STOP and report.
```
