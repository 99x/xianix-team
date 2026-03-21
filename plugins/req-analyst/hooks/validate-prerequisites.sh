#!/usr/bin/env bash
# validate-prerequisites.sh
# Validates that the environment is ready for requirement analysis.
# Run as a PreToolUse hook before Bash tool executions.

set -euo pipefail

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | grep -o '"command":"[^"]*"' | head -1 | cut -d'"' -f4 2>/dev/null || echo "")

# Only validate git commands (used for platform detection)
if ! echo "$COMMAND" | grep -qE "^git "; then
    exit 0
fi

# Check: git is available
if ! command -v git > /dev/null 2>&1; then
    echo '{"decision": "block", "reason": "git is not installed or not in PATH."}'
    exit 0
fi

# All checks passed — allow the command to proceed
exit 0
