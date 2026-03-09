#!/usr/bin/env bash
# validate-prerequisites.sh
# Validates that the environment is ready for PR review operations.
# Run as a PreToolUse hook before Bash tool executions.

set -euo pipefail

# Only run validation for PR review related commands
# Read the tool input from stdin (Claude Code passes it as JSON)
INPUT=$(cat)
COMMAND=$(echo "$INPUT" | grep -o '"command":"[^"]*"' | head -1 | cut -d'"' -f4 2>/dev/null || echo "")

# Skip validation for non-git commands
if ! echo "$COMMAND" | grep -qE "(git|gh pr|git diff|git log)"; then
    exit 0
fi

# Check: must be inside a git repository
if ! git rev-parse --is-inside-work-tree > /dev/null 2>&1; then
    echo '{"decision": "block", "reason": "Not inside a git repository. PR review requires a git project."}'
    exit 0
fi

# Check: git is available
if ! command -v git > /dev/null 2>&1; then
    echo '{"decision": "block", "reason": "git is not installed or not in PATH."}'
    exit 0
fi

# For GitHub operations (gh pr view, gh pr review), check gh CLI
if echo "$COMMAND" | grep -q "^gh "; then
    if ! command -v gh > /dev/null 2>&1; then
        echo '{"decision": "block", "reason": "GitHub CLI (gh) is not installed. Install from https://cli.github.com/ to use GitHub integration."}'
        exit 0
    fi

    if ! gh auth status > /dev/null 2>&1; then
        echo '{"decision": "block", "reason": "GitHub CLI is not authenticated. Run: gh auth login"}'
        exit 0
    fi
fi

# All checks passed — allow the command to proceed
exit 0
