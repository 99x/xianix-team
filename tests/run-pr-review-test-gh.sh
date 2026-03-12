#!/usr/bin/env bash
# run-pr-review-test-gh.sh
# Quick smoke-test driver for GitHub PRs. Loads credentials from .env,
# then delegates to run-pr-review.sh with PLATFORM=github.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/../AgentTeam.Console/.env"

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
export REPO_URL="${REPO_URL:-https://github.com/XiansAiPlatform/XiansAi.Server.git}"
export PR_NUMBER="${PR_NUMBER:-373}"

exec "${SCRIPT_DIR}/../scripts/run-pr-review.sh" "$@"
