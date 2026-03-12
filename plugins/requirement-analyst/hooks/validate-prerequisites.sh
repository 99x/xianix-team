#!/usr/bin/env bash
# validate-prerequisites.sh
# Validates that the environment is ready for requirement analysis operations.
# Run as a PreToolUse hook before Bash tool executions.
#
# Reading  — handled via GitHub MCP server (always fresh, no local git needed for issues)
# Codebase — requires local git for searching code context
#
# Credentials
#   GITHUB_TOKEN — used by the GitHub MCP server for API access (issues + file contents)
#   GIT_TOKEN    — not required for this plugin (no push/pull operations)

set -euo pipefail

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | grep -o '"command":"[^"]*"' | head -1 | cut -d'"' -f4 2>/dev/null || echo "")

# Only validate git commands
if ! echo "$COMMAND" | grep -qE "^git "; then
    exit 0
fi

# Check: git is available
if ! command -v git > /dev/null 2>&1; then
    echo '{"decision": "block", "reason": "git is not installed or not in PATH."}'
    exit 0
fi

# Check: must be inside a git repository (needed for codebase analysis)
if ! git rev-parse --is-inside-work-tree > /dev/null 2>&1; then
    echo '{"decision": "block", "reason": "Not inside a git repository. Requirement analysis requires a git project for codebase context."}'
    exit 0
fi

# All checks passed — allow the command to proceed
exit 0
