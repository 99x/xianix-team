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
#   REPO_CACHE_DIR    Directory for the shared bare clone cache (default: /tmp/requirement-analysis-cache/<repo-slug>)
#   WORKDIR           Per-run worktree directory (default: /tmp/requirement-analysis-<ISSUE_NUMBER>-<timestamp>)
#   KEEP_WORKDIR      Set to "1" to preserve the worktree after the run (for debugging)
#   GIT_FETCH_DEPTH   Shallow clone depth (default: 50)
#   GIT_RETRY_COUNT   Number of retries for network operations (default: 3)
#   GIT_RETRY_DELAY   Seconds between retries (default: 5)
#   LOCK_TIMEOUT      Seconds to wait for cache lock before aborting (default: 120)
#   REQUIREMENT_ANALYSIS_XIANIX_LOCK_DIR  Directory used for flock around the shared xianix-team plugin clone
#                                         (default: <parent of XIANIX_CACHE_DIR>/.xianix-plugin-lock)
#   REQUIREMENT_ANALYSIS_SKIP_ISSUE_COMMENTS  Set to "1" to skip GitHub / Azure DevOps issue comments (local dev)

set -euo pipefail

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

readonly SCRIPT_NAME="run-requirement-analysis"
readonly FETCH_DEPTH="${GIT_FETCH_DEPTH:-50}"
readonly RETRY_COUNT="${GIT_RETRY_COUNT:-3}"
readonly RETRY_DELAY="${GIT_RETRY_DELAY:-5}"
readonly LOCK_TIMEOUT="${LOCK_TIMEOUT:-120}"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

LOCK_FD=9
LOCK_FILE=""

log()  { echo "[${SCRIPT_NAME}] $*"; }
warn() { echo "[${SCRIPT_NAME}] WARN: $*" >&2; }

# Set by fail() so the EXIT trap can post the same message to the issue.
LAST_ERROR_MESSAGE=""

fail() {
    echo "[${SCRIPT_NAME}] ERROR: $*" >&2
    LAST_ERROR_MESSAGE="$*"
    exit 1
}

# Encode arbitrary text as a JSON string (for API bodies).
json_encode() {
    printf '%s' "$1" | python3 -c 'import json,sys; print(json.dumps(sys.stdin.read()))' 2>/dev/null
}

# Best-effort comment on the issue / work item. Skips when env is incomplete or REQUIREMENT_ANALYSIS_SKIP_ISSUE_COMMENTS=1.
post_issue_markdown_comment() {
    local _body="$1"
    [ "${REQUIREMENT_ANALYSIS_SKIP_ISSUE_COMMENTS:-0}" != "1" ] || { log "Skipping issue comment (REQUIREMENT_ANALYSIS_SKIP_ISSUE_COMMENTS=1)"; return 0; }
    [ -n "${PLATFORM:-}" ] && [ -n "${ISSUE_NUMBER:-}" ] && [ -n "${REPO_URL:-}" ] || return 0

    local _json_body
    _json_body=$(json_encode "$_body") || return 0

    case "$PLATFORM" in
        github)
            [ -n "${GITHUB_TOKEN:-}" ] || return 0
            local _repo_path
            _repo_path=$(printf '%s' "$REPO_URL" | sed 's|https://github.com/||; s|\.git$||')
            log "Posting comment to GitHub issue #${ISSUE_NUMBER}"
            curl -s -o /dev/null \
                -X POST \
                -H "Authorization: token ${GITHUB_TOKEN}" \
                -H "Content-Type: application/json" \
                -H "Accept: application/vnd.github+json" \
                -d "{\"body\": ${_json_body}}" \
                "https://api.github.com/repos/${_repo_path}/issues/${ISSUE_NUMBER}/comments" \
                || warn "Failed to post comment to GitHub issue #${ISSUE_NUMBER}"
            ;;
        azure-devops)
            [ -n "${AZURE_TOKEN:-}" ] || return 0
            local _parts _org _project _b64
            _parts=$(printf '%s' "$REPO_URL" | sed 's|https://dev.azure.com/||')
            _org=$(printf '%s' "$_parts" | cut -d'/' -f1)
            _project=$(printf '%s' "$_parts" | cut -d'/' -f2)
            _b64=$(printf ':%s' "$AZURE_TOKEN" | base64 | tr -d '\n')
            log "Posting comment to Azure DevOps work item #${ISSUE_NUMBER}"
            curl -s -o /dev/null \
                -X POST \
                -H "Authorization: Basic ${_b64}" \
                -H "Content-Type: application/json" \
                -d "{\"text\": ${_json_body}}" \
                "https://dev.azure.com/${_org}/${_project}/_apis/wit/workitems/${ISSUE_NUMBER}/comments?api-version=7.1-preview.3" \
                || warn "Failed to post comment to Azure DevOps work item #${ISSUE_NUMBER}"
            ;;
    esac
}

post_issue_start_comment() {
    local _msg
    _msg=$(printf '%s\n\n%s\n\n%s' \
        ':hourglass: **Requirement analysis started**' \
        'Automated requirement analysis is running for this issue.' \
        "_Posted automatically by \`${SCRIPT_NAME}\`._")
    post_issue_markdown_comment "$_msg"
}

# Called from EXIT trap on non-zero exit. Optional explicit message from fail(); otherwise generic.
post_issue_error_comment() {
    local _rc="${1:-1}"
    local _cause="${2:-}"
    local _msg
    if [ -n "$_cause" ]; then
        _msg=$(printf '%s\n\n%s\n\n%s\n\n%s' \
            ':x: **Requirement analysis failed**' \
            "The automated run stopped with exit code **${_rc}**." \
            "**Cause:** $(printf '%s' "$_cause")" \
            "_Posted automatically by \`${SCRIPT_NAME}\`._")
    else
        _msg=$(printf '%s\n\n%s\n\n%s\n\n%s' \
            ':x: **Requirement analysis failed**' \
            "The automated run stopped with exit code **${_rc}**." \
            'See server logs for this run for full details.' \
            "_Posted automatically by \`${SCRIPT_NAME}\`._")
    fi
    post_issue_markdown_comment "$_msg"
}

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

# Acquire an advisory flock on the parent of the cache path to prevent concurrent mutation.
acquire_cache_lock() {
    local lock_dir="$1"
    mkdir -p "$lock_dir"
    LOCK_FILE="${lock_dir}.lock"
    eval "exec ${LOCK_FD}>\"${LOCK_FILE}\""
    if ! flock --exclusive --timeout "${LOCK_TIMEOUT}" "${LOCK_FD}" 2>/dev/null; then
        if command -v flock > /dev/null 2>&1; then
            fail "Timed out waiting ${LOCK_TIMEOUT}s for cache lock: ${LOCK_FILE}"
        fi
        warn "flock not available — proceeding without cache lock (concurrent runs may race)"
    fi
}

release_cache_lock() {
    eval "exec ${LOCK_FD}>&-" 2>/dev/null || true
    [ -n "${LOCK_FILE}" ] && rm -f "${LOCK_FILE}" 2>/dev/null || true
}

# Clone the shared xianix-team plugin repo with retries. If another concurrent run
# wins the race and leaves a valid repo at XIANIX_CACHE_DIR, pull instead of failing.
_clone_xianix_plugin_repo() {
    local attempt=1
    mkdir -p "$(dirname "${XIANIX_CACHE_DIR}")"
    while [ "$attempt" -le "$RETRY_COUNT" ]; do
        if git clone --depth=1 --quiet "${XIANIX_REPO}" "${XIANIX_CACHE_DIR}"; then
            return 0
        fi
        if git -C "${XIANIX_CACHE_DIR}" rev-parse --git-dir &>/dev/null 2>&1; then
            log "xianix-team plugin repo already present (concurrent run); pulling latest"
            retry git -C "${XIANIX_CACHE_DIR}" pull --ff-only --quiet
            return 0
        fi
        if [ "$attempt" -ge "$RETRY_COUNT" ]; then
            return 1
        fi
        warn "Attempt ${attempt}/${RETRY_COUNT} failed — retrying in ${RETRY_DELAY}s..."
        sleep "$RETRY_DELAY"
        attempt=$(( attempt + 1 ))
    done
    return 1
}

cleanup_on_exit() {
    local _rc=$?
    release_cache_lock || true
    [ "$_rc" -eq 0 ] && return 0
    post_issue_error_comment "$_rc" "${LAST_ERROR_MESSAGE:-}" || true
}
trap cleanup_on_exit EXIT

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

# Normalize Azure DevOps remote URLs that embed the org name as a username
# (e.g. https://OrgName@dev.azure.com/...) — the embedded username causes
# "Bad hostname" errors when we later inject a token credential.
if [ "$PLATFORM" = "azure-devops" ]; then
    REPO_URL=$(echo "$REPO_URL" | sed 's|https://[^@]*@dev\.azure\.com|https://dev.azure.com|')
fi

XIANIX_REPO="${XIANIX_REPO:-https://github.com/99x/xianix-team.git}"
WORKDIR="${WORKDIR:-/tmp/requirement-analysis-${ISSUE_NUMBER}-$(date +%s)}"

# Filesystem-safe slug: strip protocol, strip .git suffix, replace / and : with -
# e.g. https://github.com/org/repo.git  →  github.com-org-repo
REPO_SLUG=$(echo "$REPO_URL" \
    | sed 's|https://||; s|\.git$||; s|[/: ]|-|g; s|%[0-9A-Fa-f][0-9A-Fa-f]|-|g')
REPO_CACHE_DIR="${REPO_CACHE_DIR:-/tmp/requirement-analysis-cache/${REPO_SLUG}}"

# ---------------------------------------------------------------------------
# Prerequisites check
# ---------------------------------------------------------------------------

for cmd in git claude python3; do
    command -v "$cmd" > /dev/null 2>&1 || fail "'${cmd}' is not installed"
done

post_issue_start_comment

# ---------------------------------------------------------------------------
# Step 1: Build / update the shared bare clone, then create a per-run worktree
# ---------------------------------------------------------------------------
#
# Token injection is done via GIT_CONFIG env vars (never written to any file)
# to avoid leaking credentials into the .git/config or shell history.
# Matches run-pr-review.sh: lock, retry, valid bare cache via HEAD, fresh clone if missing.

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
git -C "${REPO_CACHE_DIR}" worktree add --detach "$WORKDIR"

cd "$WORKDIR"

# Checkout the default branch (provides git context for platform detection)
DEFAULT_BRANCH=$(git -C "${REPO_CACHE_DIR}" symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's|refs/remotes/origin/||' || echo "main")
git checkout "${DEFAULT_BRANCH}" 2>/dev/null || git checkout main 2>/dev/null || log "Warning: could not checkout default branch"

log "Worktree ready on ${DEFAULT_BRANCH}"

# ---------------------------------------------------------------------------
# Step 2: Write MCP config (never committed, written to home dir for session)
# ---------------------------------------------------------------------------

log "Writing MCP config"

mkdir -p ~/.claude

# DuckDuckGo web search (no API key) — always enabled for domain/competitive context
DDG_MCP='"ddg_search": {
      "command": "npx",
      "args": ["-y", "@oevortex/ddg_search@latest"]
    }'

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
    ${DDG_MCP}
  }
}
EOF
        ;;
    azure-devops)
        export AZURE_TOKEN="${AZURE_TOKEN}"
        cat > ~/.claude/mcp-config-requirement-analysis.json <<EOF
{
  "mcpServers": {
    ${DDG_MCP}
  }
}
EOF
        ;;
esac

# ---------------------------------------------------------------------------
# Step 3: Clone / update the xianix-team plugin repo locally
# ---------------------------------------------------------------------------

XIANIX_CACHE_DIR="${XIANIX_CACHE_DIR:-/tmp/requirement-analysis-cache/xianix-team}"
PLUGIN_DIR="${XIANIX_CACHE_DIR}/plugins/req-analyst"
XIANIX_PLUGIN_LOCK_DIR="${REQUIREMENT_ANALYSIS_XIANIX_LOCK_DIR:-$(dirname "${XIANIX_CACHE_DIR}")/.xianix-plugin-lock}"

if [ "${XIANIX_USE_LOCAL:-0}" = "1" ]; then
    log "Using local xianix-team at ${XIANIX_CACHE_DIR} (XIANIX_USE_LOCAL=1)"
else
    acquire_cache_lock "${XIANIX_PLUGIN_LOCK_DIR}"

    if git -C "${XIANIX_CACHE_DIR}" rev-parse --git-dir &>/dev/null 2>&1; then
        log "Updating xianix-team plugin repo at ${XIANIX_CACHE_DIR}"
        retry git -C "${XIANIX_CACHE_DIR}" pull --ff-only --quiet
    else
        if [ -d "${XIANIX_CACHE_DIR}" ]; then
            warn "Removing incomplete/corrupt xianix-team cache at ${XIANIX_CACHE_DIR}"
            rm -rf "${XIANIX_CACHE_DIR}"
        fi
        log "Cloning xianix-team plugin repo to ${XIANIX_CACHE_DIR}"
        if ! _clone_xianix_plugin_repo; then
            release_cache_lock
            fail "Could not clone xianix-team plugin repo to ${XIANIX_CACHE_DIR}"
        fi
    fi

    release_cache_lock
fi

[ -d "${PLUGIN_DIR}" ] || fail "Plugin directory not found at ${PLUGIN_DIR} — check XIANIX_REPO"
log "Plugin ready at ${PLUGIN_DIR}"

# ---------------------------------------------------------------------------
# Step 4: Run the requirement analysis
# ---------------------------------------------------------------------------

ANALYSIS_PROMPT="/analyze-requirement ${ISSUE_NUMBER} ${COMMENT_FLAG}"
log "Running: ${ANALYSIS_PROMPT}"

set +e
CLAUDE_OUTPUT=$(claude \
    --dangerously-skip-permissions \
    --verbose \
    --plugin-dir "${PLUGIN_DIR}" \
    --mcp-config "${HOME}/.claude/mcp-config-requirement-analysis.json" \
    -p "${ANALYSIS_PROMPT}" 2>&1)
CLAUDE_EXIT=$?
set -e
printf '%s\n' "$CLAUDE_OUTPUT"
if [ "$CLAUDE_EXIT" -ne 0 ]; then
    if printf '%s' "$CLAUDE_OUTPUT" | grep -qi "credit balance is too low\|insufficient.*credit\|billing\|payment"; then
        LAST_ERROR_MESSAGE="Claude API credit balance is too low — top up your Anthropic account at https://console.anthropic.com/settings/billing"
    else
        LAST_ERROR_MESSAGE="claude exited with code ${CLAUDE_EXIT}"
    fi
    exit "$CLAUDE_EXIT"
fi

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
