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
#   PR_NUMBER         Pull request number in the target repo (from …/pull/NN). Not always the same as an issue number.
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
# Azure DevOps-specific (when PR refs unavailable, set by agent from API):
#   PR_SOURCE_REF     Full git ref for PR source branch (e.g. refs/heads/feature-branch)
#
# Optional:
#   XIANIX_REPO       Xianix plugin marketplace repo (default: https://github.com/99x/xianix-team.git)
#   XIANIX_CACHE_DIR  Local path for the cloned xianix-team repo (default: /tmp/pr-review-cache/xianix-team)
#   REPO_CACHE_DIR    Directory for the shared bare clone cache (default: /tmp/pr-review-cache/<repo-slug>)
#   WORKDIR           Per-run worktree directory (default: /tmp/pr-review-<PR_NUMBER>-<timestamp>-<pid>)
#   FIX_MODE          Set to "1" or "true" to apply and push fixes (same as --fix flag)
#   KEEP_WORKDIR      Set to "1" to preserve the worktree after the run (for debugging)
#   GIT_FETCH_DEPTH   Shallow clone depth (default: 50)
#   GIT_RETRY_COUNT   Number of retries for network operations (default: 3)
#   GIT_RETRY_DELAY   Seconds between retries (default: 5)
#   LOCK_TIMEOUT      Seconds to wait for cache lock before aborting (default: 120)
#   PR_REVIEW_SKIP_PR_COMMENTS  Set to "1" to skip GitHub / Azure DevOps PR comments (local dev)
#   PR_REVIEW_XIANIX_LOCK_DIR   Directory used for flock around the shared xianix-team plugin clone
#                               (default: <parent of XIANIX_CACHE_DIR>/.xianix-plugin-lock)

set -euo pipefail

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

readonly SCRIPT_NAME="run-pr-review"
readonly FETCH_DEPTH="${GIT_FETCH_DEPTH:-50}"
readonly RETRY_COUNT="${GIT_RETRY_COUNT:-3}"
readonly RETRY_DELAY="${GIT_RETRY_DELAY:-5}"
readonly LOCK_TIMEOUT="${LOCK_TIMEOUT:-120}"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

log()  { echo "[${SCRIPT_NAME}] $*"; }
warn() { echo "[${SCRIPT_NAME}] WARN: $*" >&2; }

# Set by fail() so the EXIT trap can post the same message to the PR.
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

# Best-effort comment on the PR. Skips when env is incomplete or PR_REVIEW_SKIP_PR_COMMENTS=1.
post_pr_markdown_comment() {
    local _body="$1"
    [ "${PR_REVIEW_SKIP_PR_COMMENTS:-0}" != "1" ] || { log "Skipping PR comment (PR_REVIEW_SKIP_PR_COMMENTS=1)"; return 0; }
    [ -n "${PLATFORM:-}" ] && [ -n "${PR_NUMBER:-}" ] && [ -n "${REPO_URL:-}" ] || return 0

    local _json_body
    _json_body=$(json_encode "$_body") || return 0

    case "$PLATFORM" in
        github)
            [ -n "${GITHUB_TOKEN:-}" ] || return 0
            local _repo_path
            _repo_path=$(printf '%s' "$REPO_URL" | sed 's|https://github.com/||; s|\.git$||')
            log "Posting comment to GitHub PR #${PR_NUMBER}"
            curl -s -o /dev/null \
                -X POST \
                -H "Authorization: token ${GITHUB_TOKEN}" \
                -H "Content-Type: application/json" \
                -H "Accept: application/vnd.github+json" \
                -d "{\"body\": ${_json_body}}" \
                "https://api.github.com/repos/${_repo_path}/issues/${PR_NUMBER}/comments" \
                || warn "Failed to post comment to GitHub PR #${PR_NUMBER}"
            ;;
        azure-devops)
            [ -n "${AZURE_TOKEN:-}" ] || return 0
            local _parts _org _project _repo_name _b64
            _parts=$(printf '%s' "$REPO_URL" | sed 's|https://dev.azure.com/||')
            _org=$(printf '%s' "$_parts" | cut -d'/' -f1)
            _project=$(printf '%s' "$_parts" | cut -d'/' -f2)
            _repo_name=$(printf '%s' "$_parts" | cut -d'/' -f4)
            _b64=$(printf ':%s' "$AZURE_TOKEN" | base64 | tr -d '\n')
            log "Posting comment to Azure DevOps PR #${PR_NUMBER}"
            curl -s -o /dev/null \
                -X POST \
                -H "Authorization: Basic ${_b64}" \
                -H "Content-Type: application/json" \
                -d "{\"comments\":[{\"content\":${_json_body},\"commentType\":1}],\"status\":1}" \
                "https://dev.azure.com/${_org}/${_project}/_apis/git/repositories/${_repo_name}/pullRequests/${PR_NUMBER}/threads?api-version=7.1" \
                || warn "Failed to post comment to Azure DevOps PR #${PR_NUMBER}"
            ;;
    esac
}

post_pr_start_comment() {
    local _msg
    _msg=$(printf '%s\n\n%s\n\n%s' \
        ':hourglass: **PR review started**' \
        'Automated PR review is running for this pull request.' \
        "_Posted automatically by \`${SCRIPT_NAME}\`._")
    post_pr_markdown_comment "$_msg"
}

# Called from cleanup EXIT trap on non-zero exit. Optional explicit message from fail(); otherwise generic.
post_pr_failure_comment() {
    local _rc="${1:-1}"
    local _cause="${2:-}"
    local _msg
    if [ -n "$_cause" ]; then
        _msg=$(printf '%s\n\n%s\n\n%s\n\n%s' \
            ':x: **PR review failed**' \
            "The automated run stopped with exit code **${_rc}**." \
            "**Cause:** $(printf '%s' "$_cause")" \
            "_Posted automatically by \`${SCRIPT_NAME}\`._")
    else
        _msg=$(printf '%s\n\n%s\n\n%s\n\n%s' \
            ':x: **PR review failed**' \
            "The automated run stopped with exit code **${_rc}**." \
            'See server logs for this run for full details.' \
            "_Posted automatically by \`${SCRIPT_NAME}\`._")
    fi
    post_pr_markdown_comment "$_msg"
}

# Fail fast with a clear message if GitHub has no such PR (refs/pull/N/head will not exist).
verify_github_pr_exists() {
    local _repo_path _code
    _repo_path=$(printf '%s' "$REPO_URL" | sed 's|https://github.com/||; s|\.git$||')
    _code=$(curl -s -o /dev/null -w "%{http_code}" \
        -H "Authorization: token ${GITHUB_TOKEN}" \
        -H "Accept: application/vnd.github+json" \
        "https://api.github.com/repos/${_repo_path}/pulls/${PR_NUMBER}")
    case "$_code" in
        200) return 0 ;;
        404)
            fail "GitHub pull request #${PR_NUMBER} was not found in ${_repo_path}. Issue numbers and PR numbers can differ — use the number from the pull request URL (…/pull/NN), not the issue list."
            ;;
        *)
            warn "Could not verify PR #${PR_NUMBER} (HTTP ${_code}); continuing with git fetch"
            ;;
    esac
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

# Acquire an advisory flock on REPO_CACHE_DIR to prevent concurrent mutation of
# the shared bare clone. The lock is released automatically when the file descriptor
# is closed (on script exit or explicit release).
LOCK_FD=9
LOCK_FILE=""
acquire_cache_lock() {
    local lock_dir="$1"
    mkdir -p "$lock_dir"
    LOCK_FILE="${lock_dir}.lock"
    eval "exec ${LOCK_FD}>\"${LOCK_FILE}\""
    if ! flock --exclusive --timeout "${LOCK_TIMEOUT}" "${LOCK_FD}" 2>/dev/null; then
        # flock may not be available on macOS; fall back gracefully
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

# ---------------------------------------------------------------------------
# Cleanup trap — runs on any exit (success, error, or signal)
# ---------------------------------------------------------------------------

WORKDIR=""
PR_BRANCH=""
REPO_CACHE_DIR_GLOBAL=""

cleanup() {
    local exit_code=$?

    if [ "$exit_code" -ne 0 ]; then
        post_pr_failure_comment "$exit_code" "${LAST_ERROR_MESSAGE:-}" || true
    fi

    release_cache_lock

    if [ "${KEEP_WORKDIR:-0}" = "1" ]; then
        log "KEEP_WORKDIR=1 — preserving worktree at ${WORKDIR}"
    elif [ -n "${WORKDIR}" ] && [ -d "${WORKDIR}" ]; then
        log "Removing worktree ${WORKDIR}"
        if [ -n "${REPO_CACHE_DIR_GLOBAL}" ] && [ -d "${REPO_CACHE_DIR_GLOBAL}" ]; then
            # Deregister from bare repo metadata and delete the directory
            git -C "${REPO_CACHE_DIR_GLOBAL}" worktree remove --force "${WORKDIR}" 2>/dev/null \
                || rm -rf "${WORKDIR}"
            git -C "${REPO_CACHE_DIR_GLOBAL}" worktree prune 2>/dev/null || true
            [ -n "${PR_BRANCH}" ] && \
                git -C "${REPO_CACHE_DIR_GLOBAL}" branch -D "${PR_BRANCH}" 2>/dev/null || true
        else
            rm -rf "${WORKDIR}"
        fi
    fi

    rm -f "${HOME}/.claude/mcp-config-pr-review.json" 2>/dev/null || true

    exit "$exit_code"
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# ---------------------------------------------------------------------------
# Parse flags
# ---------------------------------------------------------------------------

FIX_FLAG=""
for arg in "$@"; do
    case "$arg" in
        --fix) FIX_FLAG="--fix" ;;
        *)     fail "Unknown argument: ${arg}" ;;
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
# (e.g. https://OrgName@dev.azure.com/...) — the embedded username causes
# "Bad hostname" errors when we later inject a token credential.
if [ "$PLATFORM" = "azure-devops" ]; then
    REPO_URL=$(echo "$REPO_URL" | sed 's|https://[^@]*@dev\.azure\.com|https://dev.azure.com|')
fi

# ---------------------------------------------------------------------------
# Derive directory paths
# ---------------------------------------------------------------------------

XIANIX_REPO="${XIANIX_REPO:-https://github.com/99x/xianix-team.git}"

# Include PID in WORKDIR to guarantee uniqueness even when two runs start in the
# same second (same timestamp) for the same PR.
WORKDIR="${WORKDIR:-/tmp/pr-review-${PR_NUMBER}-$(date +%s)-$$}"

# Filesystem-safe slug: strip protocol, strip .git suffix, replace / and : with -
# e.g. https://github.com/org/repo.git  →  github.com-org-repo
REPO_SLUG=$(echo "$REPO_URL" \
    | sed 's|https://||; s|\.git$||; s|[/: ]|-|g; s|%[0-9A-Fa-f][0-9A-Fa-f]|-|g')
REPO_CACHE_DIR="${REPO_CACHE_DIR:-/tmp/pr-review-cache/${REPO_SLUG}}"
REPO_CACHE_DIR_GLOBAL="${REPO_CACHE_DIR}"

XIANIX_CACHE_DIR="${XIANIX_CACHE_DIR:-/tmp/pr-review-cache/xianix-team}"
PLUGIN_DIR="${XIANIX_CACHE_DIR}/plugins/pr-reviewer"

# ---------------------------------------------------------------------------
# Prerequisites check
# ---------------------------------------------------------------------------

for cmd in git claude python3; do
    command -v "$cmd" > /dev/null 2>&1 || fail "'${cmd}' is not installed"
done

post_pr_start_comment

# ---------------------------------------------------------------------------
# Step 1: Build / update the shared bare clone, then create a per-run worktree
# ---------------------------------------------------------------------------
#
# The bare clone at REPO_CACHE_DIR is the shared git object store.
# It is never checked out — worktrees provide the actual working directories.
# Multiple concurrent reviews against the same repo all share one object store
# with no conflicts, because each has its own isolated worktree.
#
# Token injection is done via GIT_CONFIG env vars (never written to any file)
# to avoid leaking credentials into the .git/config or shell history.

# Set up ephemeral token-based authentication via environment variables so the
# token is never written to disk or stored in the bare repo's remote config.
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
    # Remove stale lock files left by previously interrupted git operations
    rm -f "${REPO_CACHE_DIR}/shallow.lock" \
          "${REPO_CACHE_DIR}/packed-refs.lock" \
          "${REPO_CACHE_DIR}/HEAD.lock"
    retry git -C "${REPO_CACHE_DIR}" fetch --prune --depth="${FETCH_DEPTH}" origin
else
    # Remove any partial/corrupt directory before cloning fresh
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
# Use PID-qualified branch name to prevent collisions with branches from
# interrupted or parallel runs for the same PR number.
PR_BRANCH="pr-${PR_NUMBER}-$$"
git -C "${REPO_CACHE_DIR}" worktree add --detach "${WORKDIR}"

cd "${WORKDIR}"

# Fetch the PR branch into this worktree and check it out.
# GitHub: refs/pull/<N>/head (source branch)
# Azure DevOps: prefer PR_SOURCE_REF (refs/heads/<branch>) when available (set
#   by agent from API); else fall back through known Azure DevOps ref patterns.
case "$PLATFORM" in
    github)
        verify_github_pr_exists
        retry git fetch origin "refs/pull/${PR_NUMBER}/head:${PR_BRANCH}" --depth="${FETCH_DEPTH}"
        ;;
    azure-devops)
        if [ -n "${PR_SOURCE_REF:-}" ]; then
            retry git fetch origin "${PR_SOURCE_REF}:${PR_BRANCH}" --depth="${FETCH_DEPTH}"
        else
            # Try the standard merge ref first, then the head ref as a fallback
            retry git fetch origin "refs/pull/${PR_NUMBER}/merge:${PR_BRANCH}" --depth="${FETCH_DEPTH}" \
                || retry git fetch origin "refs/pullrequest/${PR_NUMBER}/head:${PR_BRANCH}" --depth="${FETCH_DEPTH}"
        fi
        ;;
esac

git checkout "${PR_BRANCH}"
log "Worktree ready on branch '${PR_BRANCH}'"

# ---------------------------------------------------------------------------
# Step 2: Configure git identity (required for commits in --fix mode)
# ---------------------------------------------------------------------------

git config user.name  "${GIT_USER_NAME}"
git config user.email "${GIT_USER_EMAIL}"

# ---------------------------------------------------------------------------
# Step 3: Write MCP config (ephemeral, in home dir, cleaned up on exit)
# ---------------------------------------------------------------------------

log "Writing MCP config"
mkdir -p "${HOME}/.claude"

case "$PLATFORM" in
    github)
        cat > "${HOME}/.claude/mcp-config-pr-review.json" <<EOF
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
        # Azure DevOps uses the REST API directly via curl — no MCP server required.
        export AZURE_TOKEN="${AZURE_TOKEN}"
        ;;
esac

# ---------------------------------------------------------------------------
# Step 4: Clone / update the xianix-team plugin repo
# ---------------------------------------------------------------------------
#
# We clone the repo directly and pass the plugin path to claude via --plugin-dir
# so this works in any environment: CI, webhook server, or local terminal.
#
# Concurrent PR reviews share XIANIX_CACHE_DIR. Without a lock, parallel git clone
# calls race and one fails with "destination path already exists".

XIANIX_PLUGIN_LOCK_DIR="${PR_REVIEW_XIANIX_LOCK_DIR:-$(dirname "${XIANIX_CACHE_DIR}")/.xianix-plugin-lock}"
acquire_cache_lock "${XIANIX_PLUGIN_LOCK_DIR}"

if git -C "${XIANIX_CACHE_DIR}" rev-parse --git-dir &>/dev/null 2>&1; then
    log "Updating xianix-team plugin repo at ${XIANIX_CACHE_DIR}"
    retry git -C "${XIANIX_CACHE_DIR}" pull --ff-only --quiet
else
    if [ -d "${XIANIX_CACHE_DIR}" ]; then
        log "WARN: Removing incomplete/corrupt xianix-team cache at ${XIANIX_CACHE_DIR}"
        rm -rf "${XIANIX_CACHE_DIR}"
    fi
    log "Cloning xianix-team plugin repo to ${XIANIX_CACHE_DIR}"
    if ! _clone_xianix_plugin_repo; then
        release_cache_lock
        fail "Could not clone xianix-team plugin repo to ${XIANIX_CACHE_DIR}"
    fi
fi

release_cache_lock

[ -d "${PLUGIN_DIR}" ] || fail "Plugin directory not found at ${PLUGIN_DIR} — check XIANIX_REPO"
log "Plugin ready at ${PLUGIN_DIR}"

# ---------------------------------------------------------------------------
# Step 5: Run the PR review
# ---------------------------------------------------------------------------

REVIEW_PROMPT="/review-pr ${PR_NUMBER} ${FIX_FLAG}"
log "Running: ${REVIEW_PROMPT}"

MCP_CONFIG_ARGS=()
if [ "$PLATFORM" = "github" ]; then
    MCP_CONFIG_ARGS=(--mcp-config "${HOME}/.claude/mcp-config-pr-review.json")
fi

set +e
CLAUDE_OUTPUT=$(claude \
    --dangerously-skip-permissions \
    --verbose \
    --plugin-dir "${PLUGIN_DIR}" \
    ${MCP_CONFIG_ARGS[@]+"${MCP_CONFIG_ARGS[@]}"} \
    -p "${REVIEW_PROMPT}" 2>&1)
CLAUDE_EXIT=$?
set -e
echo "$CLAUDE_OUTPUT"
if [ "$CLAUDE_EXIT" -ne 0 ]; then
    if echo "$CLAUDE_OUTPUT" | grep -qi "credit balance is too low\|insufficient.*credit\|billing\|payment"; then
        LAST_ERROR_MESSAGE="Claude API credit balance is too low — top up your Anthropic account at https://console.anthropic.com/settings/billing"
    else
        LAST_ERROR_MESSAGE="claude exited with code ${CLAUDE_EXIT}"
    fi
    exit "$CLAUDE_EXIT"
fi

log "Review complete"
