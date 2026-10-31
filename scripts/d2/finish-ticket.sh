#!/usr/bin/env bash
# Usage: bash scripts/d2/finish-ticket.sh PLT-201
# Runs the gates, verifies progress status, merges into the work branch and tags. Refuses on any failure.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=/dev/null
source "$here/d2.conf"
id="${1:?usage: finish-ticket.sh <TICKET-ID>}"
[[ "$id" =~ ^(PLT|CST|CSH)-[0-9]{3}$ ]] || { echo "bad ticket id: $id"; exit 2; }
idl="$(echo "$id" | tr 'A-Z' 'a-z')"
[ "$(git rev-parse --abbrev-ref HEAD)" = "feat/$idl" ] || { echo "Not on feat/$idl"; exit 1; }
if ! git diff --quiet || ! git diff --cached --quiet || [ -n "$(git ls-files --others --exclude-standard)" ]; then
  echo "Uncommitted changes. Commit everything first (checkpoint.sh)."; exit 1
fi
[ -n "${TEST_CMD}" ] || { echo "TEST_CMD is empty in scripts/d2/d2.conf. Fill it during bootstrap."; exit 1; }

echo "== lane check =="; bash "$here/check-lane.sh" branch

echo "== forbidden test patterns in added lines =="
base="$WORK_BRANCH"; git rev-parse --verify -q "$base" >/dev/null || base="$BASE_BRANCH"
if git diff "$base"...HEAD -- '*.cs' | grep -E '^\+' | grep -Ev '^\+\+\+' | grep -E 'Skip *=|\[Ignore|Category", *"Pending"' >/dev/null; then
  echo "Found skipped/ignored/pending tests in this ticket. Remove them (or mark the ticket BLOCKED)."; exit 1
fi

run() { local name="$1" cmd="$2"; [ -z "$cmd" ] && { echo "(skip $name: no command configured)"; return 0; }; echo "== $name =="; bash -c "$cmd"; }
run build "$BUILD_CMD"; run tests "$TEST_CMD"; run architecture "$ARCH_CMD"
run contract "$CONTRACT_CMD"; run isolation "$ISOLATION_CMD"; run migrations "$MIGRATION_CMD"
run audit "${AUDIT_CMD:-}"

echo "== progress status =="
pf="docs/team/D2-PROGRESS.md"
section="$(awk -v id="$id" '$0 ~ "^### "id" " {f=1; next} /^### / {f=0} f' "$pf")"
echo "$section" | grep -E '\*\*Status:\*\* *(DONE|DONE_WITH_FAKES)\b' >/dev/null \
  || { echo "D2-PROGRESS.md: section '### $id' must contain '**Status:** DONE' (or DONE_WITH_FAKES)."; exit 1; }

git switch "$WORK_BRANCH"
git merge --no-ff "feat/$idl" -m "merge: $id"
git tag "d2-$id-done"
echo "DONE: $id merged into $WORK_BRANCH and tagged d2-$id-done."
echo "If this is a HIGH-risk ticket or the pilot, STOP now and report for human review (Runbook section 13)."
