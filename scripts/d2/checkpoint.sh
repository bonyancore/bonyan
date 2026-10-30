#!/usr/bin/env bash
# Usage: bash scripts/d2/checkpoint.sh "feat(PLT-201): schema + tests"
# Stages everything (respecting .gitignore), checks the lane, commits. Run after every green slice,
# and update docs/team/D2-PROGRESS.md BEFORE calling it so the progress file is in the same commit.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=/dev/null
source "$here/d2.conf"
msg="${1:?usage: checkpoint.sh \"commit message\"}"
branch="$(git rev-parse --abbrev-ref HEAD)"
if [ "$branch" = "$BASE_BRANCH" ] || [ "$branch" = "main" ] || [ "$branch" = "master" ]; then
  echo "Refusing to commit on '$branch'. Run: bash scripts/d2/start-ticket.sh <TICKET-ID>"; exit 1
fi
git add -A
if git diff --cached --name-only | grep -E '(^|/)(bin|obj|node_modules)/' >/dev/null; then
  echo "Build output (bin/obj/node_modules) is staged. Fix .gitignore (in your lane only if needed), unstage, retry:"
  git diff --cached --name-only | grep -E '(^|/)(bin|obj|node_modules)/' | head -5
  git reset -q; exit 1
fi
bash "$here/check-lane.sh" staged || { git reset -q; exit 1; }
git commit -m "$msg"
git log --oneline -1
