#!/usr/bin/env bash
# Usage: bash audit/run_audit.sh [all|api|db]      (run from the repository root or from audit/)
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
[ -f config.env ] && { set -a; source config.env; set +a; }
what="${1:-all}"; rc=0
command -v python3 >/dev/null || { echo "python3 is required"; exit 2; }
python3 -c "import pytest,requests,yaml,psycopg" 2>/dev/null || { echo "Install dependencies: pip install -r audit/requirements.txt"; exit 2; }
if [ "$what" = all ] || [ "$what" = api ]; then
  echo "================ API AUDIT ================"
  python3 -m pytest api -q -p no:cacheprovider || rc=1
fi
if [ "$what" = all ] || [ "$what" = db ]; then
  echo "================ DATABASE AUDIT ================"
  python3 -m pytest db -q -p no:cacheprovider || rc=1
fi
echo
[ $rc -eq 0 ] && echo "AUDIT RESULT: no failures. Read the SKIPPED lines above: skipped areas are NOT verified (use AUDIT_STRICT=1 to fail on them)." \
              || echo "AUDIT RESULT: FAILURES found. Fix the cause in the code (never weaken the audit)."
exit $rc
