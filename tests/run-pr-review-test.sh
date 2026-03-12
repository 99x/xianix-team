#!/usr/bin/env bash
# Source .env for GIT_USER_NAME, GIT_USER_EMAIL, GITHUB_TOKEN
SCRIPT_DIR="$(dirname "$0")"
ENV_FILE="$(cd "${SCRIPT_DIR}/.." && pwd)/AgentTeam.Console/.env"
if [ -f "${ENV_FILE}" ]; then
  set -a
  # shellcheck source=/dev/null
  source "${ENV_FILE}"
  set +a
fi

export PLATFORM=github
export REPO_URL=https://github.com/XiansAiPlatform/XiansAi.Server.git
export PR_NUMBER=373

"${SCRIPT_DIR}/../scripts/run-pr-review.sh"
