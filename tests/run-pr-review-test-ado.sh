#!/usr/bin/env bash
# run-pr-review-test-ado.sh
# Quick smoke-test driver for Azure DevOps PRs. Loads credentials from .env,
# then delegates to run-pr-review.sh with PLATFORM=azure-devops.
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

: "${AZURE_TOKEN:?AZURE_TOKEN must be set in .env or environment}"

export PLATFORM=azure-devops
export REPO_URL="${REPO_URL:-https://dev.azure.com/HasithY/Codalytix%20Test/_git/Codalytix%20Test}"
export PR_NUMBER="${PR_NUMBER:-10}"

exec "${SCRIPT_DIR}/../scripts/run-pr-review.sh" "$@"
