#!/usr/bin/env bash
# notify-elaboration.sh
# PostToolUse hook — runs after every Bash tool execution.
# Provides confirmation context after codebase analysis operations.

set -euo pipefail

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | grep -o '"command":"[^"]*"' | head -1 | cut -d'"' -f4 2>/dev/null || echo "")

# Only act on git commands used for context gathering
if ! echo "$COMMAND" | grep -qE "^git (log|show|diff)"; then
    exit 0
fi

BRANCH=$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "unknown branch")

echo "Codebase context gathered from branch '${BRANCH}'."
echo "Next step: compile findings and post elaboration to GitHub issue."
