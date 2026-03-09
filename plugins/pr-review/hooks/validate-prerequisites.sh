#!/usr/bin/env bash
# validate-prerequisites.sh
# Validates that the environment is ready for PR review operations.
# Run as a PreToolUse hook before Bash tool executions.
#
# Reading  — handled via GitHub MCP server (always fresh, no local git needed)
# Writing  — requires local git for commit/push; validated here
#
# Credentials
#   GITHUB_TOKEN — used by the GitHub MCP server for API access
#   GIT_TOKEN    — used by git push/pull for HTTPS authentication (per-session,
#                  injected via GIT_CONFIG env vars, never written to disk)

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

# Check: must be inside a git repository
if ! git rev-parse --is-inside-work-tree > /dev/null 2>&1; then
    echo '{"decision": "block", "reason": "Not inside a git repository. PR review requires a git project."}'
    exit 0
fi

# For commit operations — require git identity to be set
if echo "$COMMAND" | grep -qE "^git commit"; then
    if [ -z "$(git config user.name 2>/dev/null)" ]; then
        echo '{"decision": "block", "reason": "git user.name is not set. Run: git config --global user.name \"Your Name\""}'
        exit 0
    fi
    if [ -z "$(git config user.email 2>/dev/null)" ]; then
        echo '{"decision": "block", "reason": "git user.email is not set. Run: git config --global user.email \"you@example.com\""}'
        exit 0
    fi
fi

# For push operations — require a remote and a GIT_TOKEN
if echo "$COMMAND" | grep -qE "^git push"; then
    if ! git remote | grep -q .; then
        echo '{"decision": "block", "reason": "No git remote configured. Add a remote with: git remote add origin <url>"}'
        exit 0
    fi

    if [ -z "${GIT_TOKEN:-}" ]; then
        echo '{"decision": "block", "reason": "GIT_TOKEN is not set. Pass it at runtime: GIT_TOKEN=ghp_xxx claude ... (see docs/git-auth.md)"}'
        exit 0
    fi

    # Inject token via env-based git config — no files written, no global config touched,
    # scoped to this shell session only. Works for any GitHub HTTPS remote.
    export GIT_CONFIG_COUNT=1
    export GIT_CONFIG_KEY_0="url.https://x-access-token:${GIT_TOKEN}@github.com/.insteadOf"
    export GIT_CONFIG_VALUE_0="https://github.com/"
fi

# All checks passed — allow the command to proceed
exit 0
