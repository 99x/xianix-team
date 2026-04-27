#!/usr/bin/env bash
# validate-prerequisites.sh
# Validates that the environment is ready for release note generation operations.
# Run as a PreToolUse hook before Bash tool executions.
#
# Credentials
#   AZURE_TOKEN  — used for Azure DevOps Work Items and Wiki REST API calls

set -euo pipefail

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | grep -o '"command":"[^"]*"' | head -1 | cut -d'"' -f4 2>/dev/null || echo "")

# Only validate curl commands targeting Azure DevOps
if ! echo "$COMMAND" | grep -q "^curl "; then
    exit 0
fi

# Check curl is available
if ! command -v curl > /dev/null 2>&1; then
    echo '{"decision": "block", "reason": "curl is not installed or not in PATH. Required for Azure DevOps REST API calls."}'
    exit 0
fi

# Validate AZURE_TOKEN for Azure DevOps API calls
if echo "$COMMAND" | grep -qE "dev\.azure\.com|visualstudio\.com"; then
    if [ -z "${AZURE_TOKEN:-}" ]; then
        echo '{"decision": "block", "reason": "AZURE_TOKEN is not set. Required for Azure DevOps Work Items and Wiki API. Set it in AgentTeam.Console/.env (see docs/platform-setup.md)."}'
        exit 0
    fi
fi

# All checks passed — allow the command to proceed
exit 0
