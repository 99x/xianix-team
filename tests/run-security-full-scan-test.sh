#!/usr/bin/env bash
# run-security-full-scan-test.sh
# Smoke-test driver for full codebase security scan. Loads credentials from .env,
# then delegates to run-security-review.sh with --full-scan.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
XIANIX_ROOT="${SCRIPT_DIR}/.."
ENV_FILE="${XIANIX_ROOT}/AgentTeam.Console/.env"

if [ -f "${ENV_FILE}" ]; then
    set -a
    # shellcheck source=/dev/null
    source "${ENV_FILE}"
    set +a
else
    echo "[test] WARNING: .env not found at ${ENV_FILE} — relying on exported environment" >&2
fi

: "${GITHUB_TOKEN:?GITHUB_TOKEN must be set in .env or environment}"

export PLATFORM=github
export REPO_URL="${REPO_URL:-https://github.com/99x-Internal/chaya.git}"
export SCAN_BRANCH="${SCAN_BRANCH:-security-review}"

# Report is written inside the worktree (scanned repo) so it can be committed back.
# Override SECURITY_REPORT_OUTPUT only if you want the report written elsewhere.

# Use local xianix-team when running from repo root (for local dev/testing)
if [ -d "${XIANIX_ROOT}/plugins/security-agent" ]; then
    export XIANIX_CACHE_DIR="${XIANIX_ROOT}"
    export XIANIX_USE_LOCAL=1
fi

exec "${XIANIX_ROOT}/scripts/run-security-review.sh" --full-scan "$@"
