#!/usr/bin/env bash
# run-impact-analysis.sh
#
# Bootstrap script for autonomous PR impact analysis on a server.
# Uses a shared bare clone (REPO_CACHE_DIR) as a git object store, then creates
# a lightweight per-run git worktree for full isolation between concurrent analyses.
# The worktree is removed after the run; the bare cache is kept and updated each time.
#
# Supports: GitHub, Azure DevOps
#
# Usage:
#   ./scripts/run-impact-analysis.sh
#
# Required environment variables (set by the calling server / CI system):
#
#   PLATFORM          github | azure-devops
#   REPO_URL          Full HTTPS clone URL of the repository to analyze
#   PR_NUMBER         PR / Pull Request ID to analyze
#
# GitHub-specific:
#   GITHUB_TOKEN      PAT with repo + pull_requests scopes (used by MCP + git)
#
# Azure DevOps-specific:
#   AZURE_TOKEN       PAT with Code (Read) + Pull Request Threads (Read & Write) scopes
#   GIT_TOKEN         PAT used for git clone/push over HTTPS (often same as AZURE_TOKEN)
#
# Git identity (set in .env):
#   GIT_USER_NAME     Git author name
#   GIT_USER_EMAIL    Git author email
#
# Azure DevOps-specific (when PR refs unavailable, set by agent from API):
#   PR_SOURCE_REF     Full git ref for PR source branch (e.g. refs/heads/feature-branch)
#
# Optional:
#   XIANIX_REPO       Xianix plugin marketplace repo (default: https://github.com/99x/xianix-team.git)
#   XIANIX_CACHE_DIR  Local path for the cloned xianix-team repo (default: /tmp/impact-analysis-cache/xianix-team)
#   REPO_CACHE_DIR    Directory for the shared bare clone cache (default: /tmp/impact-analysis-cache/<repo-slug>)
#   WORKDIR           Per-run worktree directory (default: /tmp/impact-analysis-<PR_NUMBER>-<timestamp>-<pid>)
#   KEEP_WORKDIR      Set to "1" to preserve the worktree after the run (for debugging)
#   GIT_FETCH_DEPTH   Shallow clone depth (default: 50)
#   GIT_RETRY_COUNT   Number of retries for network operations (default: 3)
#   GIT_RETRY_DELAY   Seconds between retries (default: 5)
#   LOCK_TIMEOUT      Seconds to wait for cache lock before aborting (default: 120)

set -euo pipefail

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

readonly SCRIPT_NAME="run-impact-analysis"
readonly FETCH_DEPTH="${GIT_FETCH_DEPTH:-50}"
readonly RETRY_COUNT="${GIT_RETRY_COUNT:-3}"
readonly RETRY_DELAY="${GIT_RETRY_DELAY:-5}"
readonly LOCK_TIMEOUT="${LOCK_TIMEOUT:-120}"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

log()  { echo "[${SCRIPT_NAME}] $*"; }
warn() { echo "[${SCRIPT_NAME}] WARN: $*" >&2; }
fail() { echo "[${SCRIPT_NAME}] ERROR: $*" >&2; exit 1; }

# Retry a command up to RETRY_COUNT times with RETRY_DELAY seconds between attempts.
retry() {
    local attempt=1
    until "$@"; do
        if [ "$attempt" -ge "$RETRY_COUNT" ]; then
            fail "Command failed after ${RETRY_COUNT} attempts: $*"
        fi
        warn "Attempt ${attempt}/${RETRY_COUNT} failed — retrying in ${RETRY_DELAY}s..."
        sleep "$RETRY_DELAY"
        attempt=$(( attempt + 1 ))
    done
}

# Acquire an advisory flock on REPO_CACHE_DIR to prevent concurrent mutation of
# the shared bare clone.
LOCK_FD=9
LOCK_FILE=""
acquire_cache_lock() {
    local lock_dir="$1"
    mkdir -p "$lock_dir"
    LOCK_FILE="${lock_dir}.lock"
    eval "exec ${LOCK_FD}>\"${LOCK_FILE}\""
    if command -v flock > /dev/null 2>&1; then
        if ! flock --exclusive --timeout "${LOCK_TIMEOUT}" "${LOCK_FD}"; then
            fail "Timed out waiting ${LOCK_TIMEOUT}s for cache lock: ${LOCK_FILE}"
        fi
    else
        warn "flock not available — proceeding without cache lock (concurrent runs may race)"
    fi
}

release_cache_lock() {
    eval "exec ${LOCK_FD}>&-" 2>/dev/null || true
    [ -n "${LOCK_FILE}" ] && rm -f "${LOCK_FILE}" 2>/dev/null || true
}

# ---------------------------------------------------------------------------
# Cleanup trap — runs on any exit (success, error, or signal)
# ---------------------------------------------------------------------------

WORKDIR=""
PR_BRANCH=""
REPO_CACHE_DIR_GLOBAL=""

cleanup() {
    local exit_code=$?

    release_cache_lock

    if [ "${KEEP_WORKDIR:-0}" = "1" ]; then
        log "KEEP_WORKDIR=1 — preserving worktree at ${WORKDIR}"
    elif [ -n "${WORKDIR}" ] && [ -d "${WORKDIR}" ]; then
        log "Removing worktree ${WORKDIR}"
        if [ -n "${REPO_CACHE_DIR_GLOBAL}" ] && [ -d "${REPO_CACHE_DIR_GLOBAL}" ]; then
            git -C "${REPO_CACHE_DIR_GLOBAL}" worktree remove --force "${WORKDIR}" 2>/dev/null \
                || rm -rf "${WORKDIR}"
            git -C "${REPO_CACHE_DIR_GLOBAL}" worktree prune 2>/dev/null || true
            [ -n "${PR_BRANCH}" ] && \
                git -C "${REPO_CACHE_DIR_GLOBAL}" branch -D "${PR_BRANCH}" 2>/dev/null || true
        else
            rm -rf "${WORKDIR}"
        fi
    fi

    exit "$exit_code"
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# ---------------------------------------------------------------------------
# Validate required environment variables
# ---------------------------------------------------------------------------

: "${PLATFORM:?PLATFORM is required (github | azure-devops)}"
: "${REPO_URL:?REPO_URL is required — full HTTPS clone URL of the target repo}"
: "${PR_NUMBER:?PR_NUMBER is required}"
: "${GIT_USER_NAME:?GIT_USER_NAME is required (set in .env)}"
: "${GIT_USER_EMAIL:?GIT_USER_EMAIL is required (set in .env)}"

# Ensure PR_NUMBER is a positive integer to guard against injection
if ! [[ "${PR_NUMBER}" =~ ^[0-9]+$ ]]; then
    fail "PR_NUMBER must be a positive integer, got: '${PR_NUMBER}'"
fi

case "$PLATFORM" in
    github)
        : "${GITHUB_TOKEN:?GITHUB_TOKEN is required for GitHub}"
        GIT_AUTH_TOKEN="${GITHUB_TOKEN}"
        ;;
    azure-devops)
        : "${AZURE_TOKEN:?AZURE_TOKEN is required for Azure DevOps}"
        GIT_TOKEN="${GIT_TOKEN:-$AZURE_TOKEN}"
        GIT_AUTH_TOKEN="${GIT_TOKEN}"
        ;;
    *)
        fail "Unknown PLATFORM '${PLATFORM}'. Supported: github, azure-devops"
        ;;
esac

# Normalize Azure DevOps remote URLs that embed the org name as a username
if [ "$PLATFORM" = "azure-devops" ]; then
    REPO_URL=$(echo "$REPO_URL" | sed 's|https://[^@]*@dev\.azure\.com|https://dev.azure.com|')
fi

# ---------------------------------------------------------------------------
# Derive directory paths
# ---------------------------------------------------------------------------

XIANIX_REPO="${XIANIX_REPO:-https://github.com/99x/xianix-team.git}"

WORKDIR="${WORKDIR:-/tmp/impact-analysis-${PR_NUMBER}-$(date +%s)-$$}"

REPO_SLUG=$(echo "$REPO_URL" \
    | sed 's|https://||; s|\.git$||; s|[/: ]|-|g; s|%[0-9A-Fa-f][0-9A-Fa-f]|-|g')
REPO_CACHE_DIR="${REPO_CACHE_DIR:-/tmp/impact-analysis-cache/${REPO_SLUG}}"
REPO_CACHE_DIR_GLOBAL="${REPO_CACHE_DIR}"

XIANIX_CACHE_DIR="${XIANIX_CACHE_DIR:-/tmp/impact-analysis-cache/xianix-team}"
PLUGIN_DIR="${XIANIX_CACHE_DIR}/plugins/imp-analyst"

# ---------------------------------------------------------------------------
# Prerequisites check
# ---------------------------------------------------------------------------

for cmd in git claude python3; do
    command -v "$cmd" > /dev/null 2>&1 || fail "'${cmd}' is not installed"
done

# ---------------------------------------------------------------------------
# Step 1: Build / update the shared bare clone, then create a per-run worktree
# ---------------------------------------------------------------------------

case "$PLATFORM" in
    github)
        export GIT_CONFIG_COUNT=1
        export GIT_CONFIG_KEY_0="url.https://x-access-token:${GIT_AUTH_TOKEN}@github.com/.insteadOf"
        export GIT_CONFIG_VALUE_0="https://github.com/"
        ;;
    azure-devops)
        AZURE_HOST=$(echo "$REPO_URL" | sed 's|https://||' | cut -d'/' -f1)
        export GIT_CONFIG_COUNT=1
        export GIT_CONFIG_KEY_0="url.https://token:${GIT_AUTH_TOKEN}@${AZURE_HOST}/.insteadOf"
        export GIT_CONFIG_VALUE_0="https://${AZURE_HOST}/"
        ;;
esac

acquire_cache_lock "${REPO_CACHE_DIR}"

if [ -f "${REPO_CACHE_DIR}/HEAD" ]; then
    log "Updating bare cache at ${REPO_CACHE_DIR}"
    rm -f "${REPO_CACHE_DIR}/shallow.lock" \
          "${REPO_CACHE_DIR}/packed-refs.lock" \
          "${REPO_CACHE_DIR}/HEAD.lock"
    retry git -C "${REPO_CACHE_DIR}" fetch --prune --depth="${FETCH_DEPTH}" origin
else
    if [ -d "${REPO_CACHE_DIR}" ]; then
        warn "Removing incomplete bare cache at ${REPO_CACHE_DIR}"
        rm -rf "${REPO_CACHE_DIR}"
    fi
    log "Creating bare cache at ${REPO_CACHE_DIR}"
    mkdir -p "$(dirname "${REPO_CACHE_DIR}")"
    retry git clone --bare --depth="${FETCH_DEPTH}" "${REPO_URL}" "${REPO_CACHE_DIR}"
fi

release_cache_lock

log "Creating isolated worktree at ${WORKDIR}"
PR_BRANCH="pr-${PR_NUMBER}-$$"
git -C "${REPO_CACHE_DIR}" worktree add --detach "${WORKDIR}"

cd "${WORKDIR}"

# Fetch the PR branch into this worktree and check it out.
case "$PLATFORM" in
    github)
        retry git fetch origin "refs/pull/${PR_NUMBER}/head:${PR_BRANCH}" --depth="${FETCH_DEPTH}"
        ;;
    azure-devops)
        if [ -n "${PR_SOURCE_REF:-}" ]; then
            retry git fetch origin "${PR_SOURCE_REF}:${PR_BRANCH}" --depth="${FETCH_DEPTH}"
        else
            retry git fetch origin "refs/pull/${PR_NUMBER}/merge:${PR_BRANCH}" --depth="${FETCH_DEPTH}" \
                || retry git fetch origin "refs/pullrequest/${PR_NUMBER}/head:${PR_BRANCH}" --depth="${FETCH_DEPTH}"
        fi
        ;;
esac

git checkout "${PR_BRANCH}"
log "Worktree ready on branch '${PR_BRANCH}'"

# ---------------------------------------------------------------------------
# Step 2: Configure git identity
# ---------------------------------------------------------------------------

git config user.name  "${GIT_USER_NAME}"
git config user.email "${GIT_USER_EMAIL}"

# ---------------------------------------------------------------------------
# Step 3: Write MCP config (ephemeral, in home dir, cleaned up on exit)
# ---------------------------------------------------------------------------

log "Writing MCP config"
MCP_CONFIG_FILE="${WORKDIR}/.mcp-config-impact-analysis.json"

case "$PLATFORM" in
    github)
        cat > "${MCP_CONFIG_FILE}" <<EOF
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
        export AZURE_TOKEN="${AZURE_TOKEN}"
        ;;
esac

# ---------------------------------------------------------------------------
# Step 4: Clone / update the xianix-team plugin repo
# ---------------------------------------------------------------------------

if [ "${XIANIX_USE_LOCAL:-0}" = "1" ]; then
    log "Using local xianix-team at ${XIANIX_CACHE_DIR} (XIANIX_USE_LOCAL=1)"
elif [ -d "${XIANIX_CACHE_DIR}/.git" ]; then
    log "Updating xianix-team plugin repo at ${XIANIX_CACHE_DIR}"
    retry git -C "${XIANIX_CACHE_DIR}" pull --ff-only --quiet
else
    log "Cloning xianix-team plugin repo to ${XIANIX_CACHE_DIR}"
    mkdir -p "$(dirname "${XIANIX_CACHE_DIR}")"
    retry git clone --depth=1 --quiet "${XIANIX_REPO}" "${XIANIX_CACHE_DIR}"
fi

[ -d "${PLUGIN_DIR}" ] || fail "Plugin directory not found at ${PLUGIN_DIR} — check XIANIX_REPO"
log "Plugin ready at ${PLUGIN_DIR}"

# ---------------------------------------------------------------------------
# Step 5: Run the impact analysis
# ---------------------------------------------------------------------------

ANALYSIS_PROMPT="/impact-analysis ${PR_NUMBER}"
log "Running: ${ANALYSIS_PROMPT}"

MCP_CONFIG_ARGS=()
if [ "$PLATFORM" = "github" ]; then
    MCP_CONFIG_ARGS=(--mcp-config "${MCP_CONFIG_FILE}")
fi

CLAUDE_OUTPUT=$(claude \
    --dangerously-skip-permissions \
    --verbose \
    --plugin-dir "${PLUGIN_DIR}" \
    ${MCP_CONFIG_ARGS[@]+"${MCP_CONFIG_ARGS[@]}"} \
    -p "${ANALYSIS_PROMPT}" 2>&1) || {
    CLAUDE_EXIT=$?
    echo "$CLAUDE_OUTPUT"
    if echo "$CLAUDE_OUTPUT" | grep -qi "credit balance is too low\|insufficient.*credit\|billing\|payment"; then
        fail "Claude API credit balance is too low — top up your Anthropic account at https://console.anthropic.com/settings/billing"
    fi
    fail "claude exited with code ${CLAUDE_EXIT}"
}
echo "$CLAUDE_OUTPUT"

log "Impact analysis complete"
