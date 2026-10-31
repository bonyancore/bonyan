#!/usr/bin/env bash
# Usage: bash scripts/d2/install-hooks.sh   (run once per clone, during bootstrap)
set -euo pipefail
git config core.hooksPath scripts/d2/hooks
chmod +x scripts/d2/*.sh scripts/d2/hooks/* 2>/dev/null || true
echo "Hooks installed: the lane check runs on commits made on d2/* and feat/(plt|cst|csh)-* branches only."
