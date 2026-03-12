#!/usr/bin/env bash
# run-pr-review.sh
#
# Bootstrap script for autonomous PR review on a server.
# Uses a shared bare clone (REPO_CACHE_DIR) as a git object store, then creates
# a lightweight per-run git worktree for full isolation between concurrent reviews.
# The worktree is removed after the run; the bare cache is kept and updated each time.
#
# Why worktrees instead of a shared checkout:
#   A single working tree cannot be used by concurrent reviews — git checkout in one
#   run would corrupt the branch state seen by another. git worktrees give each run
#   its own independent checkout with zero re-download cost after the first run.
#
# Supports: GitHub, Azure DevOps
#
# Usage:
#   ./scripts/run-pr-review.sh [--fix]
#
# Required environment variables (set by the calling server / CI system):
#
#   PLATFORM          github | azure-devops
#   REPO_URL          Full HTTPS clone URL of the repository to review
#   PR_NUMBER         PR / Pull Request ID to review
#
# GitHub-specific:
#   GITHUB_TOKEN      PAT with repo + pull_requests scopes (used by MCP + git push)
#
# Azure DevOps-specific:
#   AZURE_TOKEN       PAT with Code (Read) + Pull Request Threads (Read & Write) scopes
#   GIT_TOKEN         PAT used for git clone/push over HTTPS (often same as AZURE_TOKEN)
#
# Git identity (required for commit in --fix mode; set in .env):
#   GIT_USER_NAME     Git author name for commits
#   GIT_USER_EMAIL    Git author email for commits
#
# Optional:
#   XIANIX_REPO       Xianix plugin marketplace repo (default: https://github.com/99x/xianix-team.git)
#   XIANIX_CACHE_DIR  Local path for the cloned xianix-team repo (default: /tmp/pr-review-cache/xianix-team)
#   REPO_CACHE_DIR    Directory for the shared bare clone cache (default: /tmp/pr-review-cache/<repo-slug>)
#   WORKDIR           Per-run worktree directory (default: /tmp/pr-review-<PR_NUMBER>-<timestamp>)
#   FIX_MODE          Set to "1" or "true" to apply and push fixes (same as --fix flag)
#   KEEP_WORKDIR      Set to "1" to preserve the worktree after the run (for debugging)

set -euo pipefail

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

log()  { echo "[run-pr-review] $*"; }
fail() { echo "[run-pr-review] ERROR: $*" >&2; exit 1; }

# ---------------------------------------------------------------------------
# Parse flags
# ---------------------------------------------------------------------------

FIX_FLAG=""
for arg in "$@"; do
    case "$arg" in
        --fix) FIX_FLAG="--fix" ;;
        *)     fail "Unknown argument: $arg" ;;
    esac
done

# FIX_MODE env var is an alternative to the --fix flag
if [ "${FIX_MODE:-}" = "1" ] || [ "${FIX_MODE:-}" = "true" ]; then
    FIX_FLAG="--fix"
fi

# ---------------------------------------------------------------------------
# Validate required environment variables
# ---------------------------------------------------------------------------

: "${PLATFORM:?PLATFORM is required (github | azure-devops)}"
: "${REPO_URL:?REPO_URL is required — full HTTPS clone URL of the target repo}"
: "${PR_NUMBER:?PR_NUMBER is required}"

case "$PLATFORM" in
    github)
        : "${GITHUB_TOKEN:?GITHUB_TOKEN is required for GitHub}"
        GIT_AUTH_TOKEN="${GITHUB_TOKEN}"
        ;;
    azure-devops)
        : "${AZURE_TOKEN:?AZURE_TOKEN is required for Azure DevOps}"
        : "${GIT_TOKEN:?GIT_TOKEN is required for Azure DevOps git clone/push}"
        GIT_AUTH_TOKEN="${GIT_TOKEN}"
        ;;
    *)
        fail "Unknown PLATFORM '${PLATFORM}'. Supported: github, azure-devops"
        ;;
esac

: "${GIT_USER_NAME:?GIT_USER_NAME is required (set in .env)}"
: "${GIT_USER_EMAIL:?GIT_USER_EMAIL is required (set in .env)}"

XIANIX_REPO="${XIANIX_REPO:-https://github.com/99x/xianix-team.git}"
WORKDIR="${WORKDIR:-/tmp/pr-review-${PR_NUMBER}-$(date +%s)}"

# Derive a filesystem-safe slug from the repo URL for the bare cache directory.
# e.g. https://github.com/org/repo.git  →  github.com-org-repo
REPO_SLUG=$(echo "$REPO_URL" | sed 's|https://||; s|\.git$||; s|[/:]|-|g')
REPO_CACHE_DIR="${REPO_CACHE_DIR:-/tmp/pr-review-cache/${REPO_SLUG}}"

# ---------------------------------------------------------------------------
# Prerequisites check
# ---------------------------------------------------------------------------

command -v git    > /dev/null 2>&1 || fail "git is not installed"
command -v claude > /dev/null 2>&1 || fail "claude CLI is not installed (https://docs.anthropic.com/claude-code)"
command -v python3 > /dev/null 2>&1 || fail "python3 is not installed"

# ---------------------------------------------------------------------------
# Step 1: Build / update the shared bare clone, then create a per-run worktree
# ---------------------------------------------------------------------------
#
# The bare clone at REPO_CACHE_DIR is the shared git object store.
# It is never checked out — worktrees provide the actual working directories.
# Multiple concurrent reviews against the same repo all share one object store
# with no conflicts, because each has its own isolated worktree.

# Build the authenticated URL (token inline, never written to disk)
case "$PLATFORM" in
    github)
        AUTH_URL=$(echo "$REPO_URL" | sed "s|https://github.com/|https://x-access-token:${GIT_AUTH_TOKEN}@github.com/|")
        ;;
    azure-devops)
        AUTH_URL=$(echo "$REPO_URL" | sed "s|https://|https://token:${GIT_AUTH_TOKEN}@|")
        ;;
esac

if [ -d "${REPO_CACHE_DIR}" ]; then
    log "Updating bare cache at ${REPO_CACHE_DIR}"
    git -C "${REPO_CACHE_DIR}" fetch --prune --depth=50 origin
else
    log "Creating bare cache at ${REPO_CACHE_DIR}"
    mkdir -p "$(dirname "${REPO_CACHE_DIR}")"
    git clone --bare --depth=50 "$AUTH_URL" "${REPO_CACHE_DIR}"
fi

log "Creating isolated worktree at ${WORKDIR}"
# Use a unique branch name per run to avoid "refusing to fetch into branch checked out" errors
# when a previous worktree (e.g. from interrupted run) still has pr-N checked out.
PR_BRANCH="pr-${PR_NUMBER}-$$"
git -C "${REPO_CACHE_DIR}" worktree add --detach "$WORKDIR"

cd "$WORKDIR"

# Fetch the PR branch into this worktree and check it out.
# For GitHub PRs, the ref is refs/pull/<N>/head. For Azure DevOps it is refs/pull/<N>/merge.
case "$PLATFORM" in
    github)
        git fetch origin "refs/pull/${PR_NUMBER}/head:${PR_BRANCH}" --depth=50
        ;;
    azure-devops)
        git fetch origin "refs/pull/${PR_NUMBER}/merge:${PR_BRANCH}" --depth=50
        ;;
esac

git checkout "${PR_BRANCH}"

log "Worktree ready on PR branch"

# ---------------------------------------------------------------------------
# Step 2: Configure git identity (required for commit in --fix mode)
# ---------------------------------------------------------------------------

git config user.name  "${GIT_USER_NAME}"
git config user.email "${GIT_USER_EMAIL}"

# Inject git token for push via env-based config (never written to disk).
# Scoped to this shell process only.
export GIT_CONFIG_COUNT=1
export GIT_CONFIG_KEY_0="url.https://x-access-token:${GIT_AUTH_TOKEN}@github.com/.insteadOf"
export GIT_CONFIG_VALUE_0="https://github.com/"

if [ "$PLATFORM" = "azure-devops" ]; then
    # For Azure DevOps, rewrite the org URL to inject the token
    AZURE_HOST=$(echo "$REPO_URL" | sed 's|https://||' | cut -d'/' -f1)
    export GIT_CONFIG_KEY_0="url.https://token:${GIT_AUTH_TOKEN}@${AZURE_HOST}/.insteadOf"
    export GIT_CONFIG_VALUE_0="https://${AZURE_HOST}/"
fi

# ---------------------------------------------------------------------------
# Step 3: Write MCP config (never committed, written to home dir for session)
# ---------------------------------------------------------------------------

log "Writing MCP config"

mkdir -p ~/.claude

case "$PLATFORM" in
    github)
        cat > ~/.claude/mcp-config-pr-review.json <<EOF
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "${GITHUB_TOKEN}"
      }
    }
  }
}
EOF
        ;;
    azure-devops)
        # Azure DevOps uses the REST API via curl — no MCP server needed.
        # AZURE_TOKEN is already exported; re-export to ensure child processes inherit it.
        export AZURE_TOKEN="${AZURE_TOKEN}"
        ;;
esac

# ---------------------------------------------------------------------------
# Step 4: Clone / update the xianix-team plugin repo locally
# ---------------------------------------------------------------------------
#
# Instead of spawning a nested `claude` session to run /plugin marketplace add
# (which fails inside an existing Claude Code session), we clone the repo
# directly and pass the plugin path to claude via --plugin-dir. This works
# in any environment — CI, webhook server, or local terminal.

XIANIX_CACHE_DIR="${XIANIX_CACHE_DIR:-/tmp/pr-review-cache/xianix-team}"
PLUGIN_DIR="${XIANIX_CACHE_DIR}/plugins/pr-review"

if [ -d "${XIANIX_CACHE_DIR}/.git" ]; then
    log "Updating xianix-team plugin repo at ${XIANIX_CACHE_DIR}"
    git -C "${XIANIX_CACHE_DIR}" pull --ff-only --quiet
else
    log "Cloning xianix-team plugin repo to ${XIANIX_CACHE_DIR}"
    mkdir -p "$(dirname "${XIANIX_CACHE_DIR}")"
    git clone --depth=1 --quiet "${XIANIX_REPO}" "${XIANIX_CACHE_DIR}"
fi

[ -d "${PLUGIN_DIR}" ] || fail "Plugin directory not found at ${PLUGIN_DIR} — check XIANIX_REPO"
log "Plugin ready at ${PLUGIN_DIR}"

# ---------------------------------------------------------------------------
# Step 5: Run the PR review
# ---------------------------------------------------------------------------

REVIEW_PROMPT="/review-pr ${PR_NUMBER} ${FIX_FLAG}"
log "Running: ${REVIEW_PROMPT}"

# --plugin-dir loads the plugin from the local filesystem — no marketplace needed.
# --verbose streams the agent's reasoning and tool calls to stdout in real time.
# For GitHub, also pass the MCP config for posting inline review comments.
if [ "$PLATFORM" = "github" ]; then
    claude \
        --dangerously-skip-permissions \
        --verbose \
        --plugin-dir "${PLUGIN_DIR}" \
        --mcp-config "${HOME}/.claude/mcp-config-pr-review.json" \
        -p "${REVIEW_PROMPT}"
else
    claude \
        --dangerously-skip-permissions \
        --verbose \
        --plugin-dir "${PLUGIN_DIR}" \
        -p "${REVIEW_PROMPT}"
fi

log "Review complete"

# ---------------------------------------------------------------------------
# Step 6: Cleanup
# ---------------------------------------------------------------------------
# Remove the per-run worktree. The bare clone cache and xianix-team plugin
# cache are kept for the next run — only the isolated per-PR worktree is removed.
# KEEP_WORKDIR=1 preserves the worktree for post-mortem debugging.

if [ "${KEEP_WORKDIR:-0}" != "1" ]; then
    log "Removing worktree ${WORKDIR}"
    # git worktree remove deregisters the worktree from the bare repo metadata
    # and deletes the directory. --force handles any leftover untracked files.
    git -C "${REPO_CACHE_DIR}" worktree remove --force "$WORKDIR" 2>/dev/null || rm -rf "$WORKDIR"
    git -C "${REPO_CACHE_DIR}" worktree prune
    git -C "${REPO_CACHE_DIR}" branch -D "${PR_BRANCH}" 2>/dev/null || true
    rm -f ~/.claude/mcp-config-pr-review.json
fi
