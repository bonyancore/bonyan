#!/usr/bin/env bash
# Usage: bash scripts/d2/start-ticket.sh PLT-201
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=/dev/null
source "$here/d2.conf"
id="${1:?usage: start-ticket.sh <TICKET-ID, e.g. PLT-201>}"
[[ "$id" =~ ^(PLT|CST|CSH)-[0-9]{3}$ ]] || { echo "bad ticket id: $id"; exit 2; }
idl="$(echo "$id" | tr 'A-Z' 'a-z')"
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Working tree has uncommitted changes. Commit them (checkpoint.sh) or review them first."; exit 1
fi
if ! git rev-parse --verify -q "$WORK_BRANCH" >/dev/null; then
  git rev-parse --verify -q "$BASE_BRANCH" >/dev/null || { echo "Base branch '$BASE_BRANCH' does not exist. Set BASE_BRANCH in scripts/d2/d2.conf (for example master or develop)."; exit 1; }
  git switch "$BASE_BRANCH"
  git switch -c "$WORK_BRANCH"
else
  git switch "$WORK_BRANCH"
fi
# warn about other unmerged ticket branches (blocked/in-progress ones)
for b in $(git for-each-ref --format='%(refname:short)' 'refs/heads/feat/*'); do
  [ "$b" = "feat/$idl" ] && continue
  if [ -n "$(git log --oneline "$WORK_BRANCH..$b" 2>/dev/null | head -1)" ]; then
    echo "NOTE: branch $b has commits not merged into $WORK_BRANCH (blocked or unfinished ticket). Make sure D2-PROGRESS.md says so."
  fi
done
if git rev-parse --verify -q "feat/$idl" >/dev/null; then git switch "feat/$idl"; else git switch -c "feat/$idl"; fi
echo "On branch feat/$idl. Next: set $id to IN_PROGRESS in docs/team/D2-PROGRESS.md (plan + slices), then:"
echo "  bash scripts/d2/checkpoint.sh \"chore(progress): start $id\""
