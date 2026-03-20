#!/usr/bin/env bash
# run-req-analyst-test-gh.sh
# Quick smoke-test driver for requirement analysis on GitHub. Loads credentials
# from .env, then delegates to run-requirement-analysis.sh with PLATFORM=github.
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
# Optional: TAVILY_API_KEY for competitive/market context (web search)

export PLATFORM=github
export REPO_URL="${REPO_URL:-https://github.com/XiansAiPlatform/agent-studio.git}"
export ISSUE_NUMBER="${ISSUE_NUMBER:-25}"

# Use local xianix-team when running from repo root (for local dev/testing)
if [ -d "${XIANIX_ROOT}/plugins/requirement-analyst" ]; then
    export XIANIX_CACHE_DIR="${XIANIX_ROOT}"
    export XIANIX_USE_LOCAL=1
fi

exec "${XIANIX_ROOT}/scripts/run-requirement-analysis.sh" "$@"
