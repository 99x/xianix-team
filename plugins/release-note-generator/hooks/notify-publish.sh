#!/usr/bin/env bash
# notify-publish.sh
# PostToolUse hook — runs after every Bash tool execution.
# If the command was a curl PUT to the Azure DevOps wiki API, outputs a publish confirmation.

set -euo pipefail

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | grep -o '"command":"[^"]*"' | head -1 | cut -d'"' -f4 2>/dev/null || echo "")

# Only act on curl PUT operations to the wiki API
if ! echo "$COMMAND" | grep -qE "^curl .*-X PUT"; then
    exit 0
fi

if echo "$COMMAND" | grep -qE "_apis/wiki/wikis/.*/pages"; then
    echo "Wiki page updated — release notes published to Azure DevOps wiki."
    echo "Next step: output the wiki URL confirmation line (see providers/azure-devops.md)."
fi
