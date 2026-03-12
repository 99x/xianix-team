#!/usr/bin/env bash
# notify-push.sh
# PostToolUse hook — runs after every Bash tool execution.
# If the command was a git push, outputs the remote branch URL for confirmation.

set -euo pipefail

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | grep -o '"command":"[^"]*"' | head -1 | cut -d'"' -f4 2>/dev/null || echo "")

# Only act on git push commands
if ! echo "$COMMAND" | grep -qE "^git push"; then
    exit 0
fi

# Resolve the remote URL and current branch for a helpful confirmation message
REMOTE=$(git remote get-url origin 2>/dev/null || echo "unknown remote")
BRANCH=$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "unknown branch")
COMMIT=$(git log -1 --oneline 2>/dev/null || echo "")

echo "Push complete — branch '${BRANCH}' pushed to ${REMOTE}"
echo "Latest commit: ${COMMIT}"

if echo "$REMOTE" | grep -q "github.com"; then
    echo "Next step: use mcp__github__create_pull_request_review to post the review (see providers/github.md)."
elif echo "$REMOTE" | grep -qE "dev.azure.com|visualstudio.com"; then
    echo "Next step: post the review via Azure DevOps REST API (see providers/azure-devops.md)."
else
    echo "Next step: write the review report to pr-review-report.md (see providers/generic.md)."
fi
