#!/usr/bin/env bash
# run-requirement-analysis.sh
#
# Bootstrap script for autonomous requirement analysis on a server.
# Uses a shared bare clone (REPO_CACHE_DIR) as a git object store, then creates
# a lightweight per-run git worktree for full isolation between concurrent analyses.
# The worktree is removed after the run; the bare cache is kept and updated each time.
#
# Supports: GitHub, Azure DevOps
#
# Usage:
#   ./scripts/run-requirement-analysis.sh [--comment]
#
# Required environment variables (set by the calling server / CI system):
#
#   PLATFORM          github | azure-devops
#   REPO_URL          Full HTTPS clone URL of the repository
#   ISSUE_NUMBER      Issue / Work Item ID to analyze
#
# GitHub-specific:
#   GITHUB_TOKEN      PAT with repo scopes (used by MCP for issue read/write)
#
# Azure DevOps-specific:
#   AZURE_TOKEN       PAT with Work Items (Read & Write) scopes
#   GIT_TOKEN         PAT used for git clone over HTTPS (often same as AZURE_TOKEN)
#
# Optional:
#   XIANIX_REPO       Xianix plugin marketplace repo (default: https://github.com/99x/xianix-team.git)
#   XIANIX_CACHE_DIR  Local path for the cloned xianix-team repo (default: /tmp/requirement-analysis-cache/xianix-team)
#   XIANIX_USE_LOCAL  Set to "1" to use XIANIX_CACHE_DIR as-is (no clone/pull) — for local dev testing
#   TAVILY_API_KEY    Tavily API key for web search (competitive/market context). Optional.
#   REPO_CACHE_DIR    Directory for the shared bare clone cache (default: /tmp/requirement-analysis-cache/<repo-slug>)
#   WORKDIR           Per-run worktree directory (default: /tmp/requirement-analysis-<ISSUE_NUMBER>-<timestamp>)
#   KEEP_WORKDIR      Set to "1" to preserve the worktree after the run (for debugging)

set -euo pipefail

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

log()  { echo "[run-requirement-analysis] $*"; }
fail() { echo "[run-requirement-analysis] ERROR: $*" >&2; exit 1; }

# ---------------------------------------------------------------------------
# Parse flags
# ---------------------------------------------------------------------------

COMMENT_FLAG=""
for arg in "$@"; do
    case "$arg" in
        --comment) COMMENT_FLAG="--comment" ;;
        *)         fail "Unknown argument: $arg" ;;
    esac
done

# ---------------------------------------------------------------------------
# Validate required environment variables
# ---------------------------------------------------------------------------

: "${PLATFORM:?PLATFORM is required (github | azure-devops)}"
: "${REPO_URL:?REPO_URL is required — full HTTPS clone URL of the target repo}"
: "${ISSUE_NUMBER:?ISSUE_NUMBER is required}"

case "$PLATFORM" in
    github)
        : "${GITHUB_TOKEN:?GITHUB_TOKEN is required for GitHub}"
        GIT_AUTH_TOKEN="${GITHUB_TOKEN}"
        ;;
    azure-devops)
        : "${AZURE_TOKEN:?AZURE_TOKEN is required for Azure DevOps}"
        : "${GIT_TOKEN:?GIT_TOKEN is required for Azure DevOps git clone}"
        GIT_AUTH_TOKEN="${GIT_TOKEN}"
        ;;
    *)
        fail "Unknown PLATFORM '${PLATFORM}'. Supported: github, azure-devops"
        ;;
esac

XIANIX_REPO="${XIANIX_REPO:-https://github.com/99x/xianix-team.git}"
WORKDIR="${WORKDIR:-/tmp/requirement-analysis-${ISSUE_NUMBER}-$(date +%s)}"

# Derive a filesystem-safe slug from the repo URL for the bare cache directory.
REPO_SLUG=$(echo "$REPO_URL" | sed 's|https://||; s|\.git$||; s|[/:]|-|g')
REPO_CACHE_DIR="${REPO_CACHE_DIR:-/tmp/requirement-analysis-cache/${REPO_SLUG}}"

# ---------------------------------------------------------------------------
# Prerequisites check
# ---------------------------------------------------------------------------

command -v git    > /dev/null 2>&1 || fail "git is not installed"
command -v claude > /dev/null 2>&1 || fail "claude CLI is not installed (https://docs.anthropic.com/claude-code)"

# ---------------------------------------------------------------------------
# Step 1: Build / update the shared bare clone, then create a per-run worktree
# ---------------------------------------------------------------------------

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
git -C "${REPO_CACHE_DIR}" worktree add --detach "$WORKDIR"

cd "$WORKDIR"

# Checkout the default branch for codebase context
DEFAULT_BRANCH=$(git -C "${REPO_CACHE_DIR}" symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's|refs/remotes/origin/||' || echo "main")
git checkout "${DEFAULT_BRANCH}" 2>/dev/null || git checkout main 2>/dev/null || log "Warning: could not checkout default branch"

log "Worktree ready on ${DEFAULT_BRANCH}"

# ---------------------------------------------------------------------------
# Step 2: Write MCP config (never committed, written to home dir for session)
# ---------------------------------------------------------------------------

log "Writing MCP config"

mkdir -p ~/.claude

# DuckDuckGo web search (no API key) — always enabled for competitive context
DDG_MCP='"ddg_search": {
      "command": "npx",
      "args": ["-y", "@oevortex/ddg_search@latest"]
    }'

# Optional Tavily server (higher quality, requires API key)
if [ -n "${TAVILY_API_KEY:-}" ]; then
    TAVILY_MCP=', "tavily": {
      "command": "npx",
      "args": ["-y", "tavily-mcp@latest"],
      "env": {
        "TAVILY_API_KEY": "'"${TAVILY_API_KEY}"'"
      }
    }'
else
    TAVILY_MCP=""
fi

case "$PLATFORM" in
    github)
        cat > ~/.claude/mcp-config-requirement-analysis.json <<EOF
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "${GITHUB_TOKEN}"
      }
    },
    ${DDG_MCP}${TAVILY_MCP}
  }
}
EOF
        ;;
    azure-devops)
        export AZURE_TOKEN="${AZURE_TOKEN}"
        cat > ~/.claude/mcp-config-requirement-analysis.json <<EOF
{
  "mcpServers": {
    ${DDG_MCP}${TAVILY_MCP}
  }
}
EOF
        ;;
esac

# ---------------------------------------------------------------------------
# Step 3: Clone / update the xianix-team plugin repo locally
# ---------------------------------------------------------------------------

XIANIX_CACHE_DIR="${XIANIX_CACHE_DIR:-/tmp/requirement-analysis-cache/xianix-team}"
PLUGIN_DIR="${XIANIX_CACHE_DIR}/plugins/requirement-analyst"

if [ "${XIANIX_USE_LOCAL:-0}" = "1" ]; then
    log "Using local xianix-team at ${XIANIX_CACHE_DIR} (XIANIX_USE_LOCAL=1)"
elif [ -d "${XIANIX_CACHE_DIR}/.git" ]; then
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
# Step 4: Run the requirement analysis
# ---------------------------------------------------------------------------

ANALYSIS_PROMPT="/analyze-requirement ${ISSUE_NUMBER} ${COMMENT_FLAG}"
log "Running: ${ANALYSIS_PROMPT}"

claude \
    --dangerously-skip-permissions \
    --verbose \
    --plugin-dir "${PLUGIN_DIR}" \
    --mcp-config "${HOME}/.claude/mcp-config-requirement-analysis.json" \
    -p "${ANALYSIS_PROMPT}"

log "Requirement analysis complete"

# ---------------------------------------------------------------------------
# Step 5: Cleanup
# ---------------------------------------------------------------------------

if [ "${KEEP_WORKDIR:-0}" != "1" ]; then
    log "Removing worktree ${WORKDIR}"
    git -C "${REPO_CACHE_DIR}" worktree remove --force "$WORKDIR" 2>/dev/null || rm -rf "$WORKDIR"
    git -C "${REPO_CACHE_DIR}" worktree prune
    rm -f ~/.claude/mcp-config-requirement-analysis.json
fi
