#!/usr/bin/env bash
# Usage: bash scripts/d2/check-lane.sh [branch|staged|working] [base-branch]
# Fails (exit 1) if any changed file is outside the D2 lane. Warns for paths that need architect review.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=/dev/null
source "$here/d2.conf"
mode="${1:-branch}"

case "$mode" in
  staged)  files="$(git diff --cached --name-only --no-renames --diff-filter=ACMRD)" ;;
  working) files="$( { git diff --name-only --no-renames; git diff --cached --name-only --no-renames; git ls-files --others --exclude-standard; } | sort -u )" ;;
  branch)
    base="${2:-$WORK_BRANCH}"
    git rev-parse --verify -q "$base" >/dev/null 2>&1 || base="$BASE_BRANCH"
    files="$( { git diff --name-only --no-renames "$base"...HEAD; git diff --name-only --no-renames; git diff --cached --name-only --no-renames; git ls-files --others --exclude-standard; } | sort -u )" ;;
  *) echo "usage: check-lane.sh [branch|staged|working] [base]"; exit 2 ;;
esac

[ -z "$files" ] && { echo "lane check: no changes"; exit 0; }

violations=(); review=()
while IFS= read -r f; do
  [ -z "$f" ] && continue
  if   [[ "$f" =~ $REVIEW_REGEX  ]]; then review+=("$f")
  elif [[ "$f" =~ $ALLOWED_REGEX ]]; then :
  else violations+=("$f"); fi
done <<< "$files"

if [ ${#review[@]} -gt 0 ]; then
  echo "LANE WARNING: these paths need ARCHITECT REVIEW (list them in the PR and in D2-PROGRESS.md):"
  printf '  - %s\n' "${review[@]}"
fi
if [ ${#violations[@]} -gt 0 ]; then
  echo "LANE VIOLATION: files outside your lane (Platform, Cost, Cash). Revert them, or use a fake/port/RFC instead:"
  printf '  - %s\n' "${violations[@]}"
  exit 1
fi
echo "lane check: OK"
