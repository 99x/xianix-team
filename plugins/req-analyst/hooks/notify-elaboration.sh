#!/usr/bin/env bash
# notify-elaboration.sh
# PostToolUse hook — runs after every Bash tool execution.

set -euo pipefail

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | grep -o '"command":"[^"]*"' | head -1 | cut -d'"' -f4 2>/dev/null || echo "")

# Only act on git remote commands (used for platform detection)
if ! echo "$COMMAND" | grep -qE "^git remote"; then
    exit 0
fi

echo "Platform detected. Next step: post elaboration to the backlog platform."
