#!/usr/bin/env bash
# run-impact-analysis-test-gh.sh
# Quick smoke-test driver for impact analysis on GitHub. Loads credentials
# from .env, then delegates to run-impact-analysis.sh with PLATFORM=github.
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
export REPO_URL="${REPO_URL:-https://github.com/XiansAiPlatform/XiansAi.Lib.git}"
export PR_NUMBER="${PR_NUMBER:-83}"
export GIT_USER_NAME="${GIT_USER_NAME:-Xianix Bot}"
export GIT_USER_EMAIL="${GIT_USER_EMAIL:-bot@xianix.ai}"

# Use local xianix-team when running from repo root (for local dev/testing)
if [ -d "${XIANIX_ROOT}/plugins/imp-analyst" ]; then
    export XIANIX_CACHE_DIR="${XIANIX_ROOT}"
    export XIANIX_USE_LOCAL=1
fi

# On Windows/Git Bash, python3 may not exist — shim it with python if needed
if ! command -v python3 > /dev/null 2>&1 && command -v python > /dev/null 2>&1; then
    SHIM_DIR="$(mktemp -d)"
    printf '#!/usr/bin/env bash\nexec python "$@"\n' > "${SHIM_DIR}/python3"
    chmod +x "${SHIM_DIR}/python3"
    export PATH="${SHIM_DIR}:${PATH}"
    trap 'rm -rf "${SHIM_DIR}"' EXIT
fi

exec "${XIANIX_ROOT}/scripts/run-impact-analysis.sh" "$@"
