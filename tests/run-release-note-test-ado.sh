#!/usr/bin/env bash
# run-release-note-test-ado.sh
# Quick smoke-test driver for Azure DevOps release notes. Loads credentials from .env,
# then delegates to run-release-notes.sh.
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
: "${SPRINT_NAME:?SPRINT_NAME must be set in .env or environment}"

# Use local plugin if running from within the repo
if [ -d "${SCRIPT_DIR}/../plugins/release-note-generator" ]; then
    export XIANIX_CACHE_DIR="${SCRIPT_DIR}/.."
    export XIANIX_USE_LOCAL=1
fi

exec "${SCRIPT_DIR}/../scripts/run-release-notes.sh" "$@"
