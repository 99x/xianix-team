#!/usr/bin/env bash
# run-security-review.sh
#
# Bootstrap script for autonomous security review on a server.
# Supports two modes:
#
#   PR review mode (default):
#     Reviews the diff of a specific pull request and posts findings to the PR.
#
#   Full-scan mode (--full-scan):
#     Clones a branch and scans the entire codebase. Writes a Markdown report to
#     SECURITY_REPORT_OUTPUT (default: SECURITY_REVIEW.md in the calling directory).
#     No PR comment is posted. PR_NUMBER is not required.
#
# Uses a shared bare clone (REPO_CACHE_DIR) as a git object store, then creates
# a lightweight per-run git worktree for full isolation between concurrent reviews.
# The worktree is removed after the run; the bare cache is kept and updated each time.
#
# Supports: GitHub, Azure DevOps
#
# Usage:
#   ./scripts/run-security-review.sh [--comment]
#   ./scripts/run-security-review.sh --full-scan
#
# Required environment variables (set by the calling server / CI system):
#
#   PLATFORM          github | azure-devops
#   REPO_URL          Full HTTPS clone URL of the repository to review
#   PR_NUMBER         Pull request number in the target repo (from …/pull/NN)
#                     — not required when --full-scan is used
#
# GitHub-specific:
#   GITHUB_TOKEN      PAT with repo + pull_requests scopes (used by MCP + git fetch)
#
# Azure DevOps-specific:
#   AZURE_TOKEN       PAT with Code (Read) + Pull Request Threads (Read & Write) scopes
#   GIT_TOKEN         PAT used for git clone over HTTPS (often same as AZURE_TOKEN)
#
# Azure DevOps-specific (when PR refs unavailable, set by agent from API):
#   PR_SOURCE_REF     Full git ref for PR source branch (e.g. refs/heads/feature-branch)
#
# Optional:
#   XIANIX_REPO            Xianix plugin marketplace repo (default: https://github.com/99x/xianix-team.git)
#   XIANIX_CACHE_DIR       Local path for the cloned xianix-team repo (default: /tmp/security-review-cache/xianix-team)
#   XIANIX_USE_LOCAL       Set to "1" to use XIANIX_CACHE_DIR as-is (no clone/pull) — for local dev testing
#   REPO_CACHE_DIR         Directory for the shared bare clone cache (default: /tmp/security-review-cache/<repo-slug>)
#   WORKDIR                Per-run worktree directory (default: /tmp/security-review-<PR_NUMBER>-<timestamp>-<pid>)
#   KEEP_WORKDIR           Set to "1" to preserve the worktree after the run (for debugging)
#   GIT_FETCH_DEPTH        Shallow clone depth (default: 50)
#   GIT_RETRY_COUNT        Number of retries for network operations (default: 3)
#   GIT_RETRY_DELAY        Seconds between retries (default: 5)
#   SECURITY_REVIEW_SKIP_PR_COMMENTS  Set to "1" to skip GitHub / Azure DevOps PR comments (local dev)
#
# Full-scan-specific optional:
#   SCAN_BRANCH            Branch to scan (default: main)
#   SECURITY_REPORT_OUTPUT Absolute path for the Markdown report (default: <calling-dir>/SECURITY_REVIEW.md)

set -euo pipefail

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

readonly SCRIPT_NAME="run-security-review"
readonly FETCH_DEPTH="${GIT_FETCH_DEPTH:-50}"
readonly RETRY_COUNT="${GIT_RETRY_COUNT:-3}"
readonly RETRY_DELAY="${GIT_RETRY_DELAY:-5}"

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

# Best-effort comment on the PR. Skips when env is incomplete or SECURITY_REVIEW_SKIP_PR_COMMENTS=1.
post_pr_markdown_comment() {
    local _body="$1"
    [ "${SECURITY_REVIEW_SKIP_PR_COMMENTS:-0}" != "1" ] || { log "Skipping PR comment (SECURITY_REVIEW_SKIP_PR_COMMENTS=1)"; return 0; }
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
        ':shield: **Security review started**' \
        'Automated security review is running for this pull request.' \
        "_Posted automatically by \`${SCRIPT_NAME}\`._")
    post_pr_markdown_comment "$_msg"
}

# Called from EXIT trap on non-zero exit.
post_pr_failure_comment() {
    local _rc="${1:-1}"
    local _cause="${2:-}"
    local _msg
    if [ -n "$_cause" ]; then
        _msg=$(printf '%s\n\n%s\n\n%s\n\n%s' \
            ':x: **Security review failed**' \
            "The automated run stopped with exit code **${_rc}**." \
            "**Cause:** $(printf '%s' "$_cause")" \
            "_Posted automatically by \`${SCRIPT_NAME}\`._")
    else
        _msg=$(printf '%s\n\n%s\n\n%s\n\n%s' \
            ':x: **Security review failed**' \
            "The automated run stopped with exit code **${_rc}**." \
            'See server logs for this run for full details.' \
            "_Posted automatically by \`${SCRIPT_NAME}\`._")
    fi
    post_pr_markdown_comment "$_msg"
}

# Fail fast with a clear message if GitHub has no such PR.
verify_github_pr_exists() {
    local _repo_path _code
    _repo_path=$(printf '%s' "$REPO_URL" | sed 's|https://github.com/||; s|\.git$||')
    _code=$(curl -s -o /dev/null -w "%{http_code}" \
        -H "Authorization: token ${GITHUB_TOKEN}" \
        -H "Accept: application/vnd.github+json" \
        "https://api.github.com/repos/${_repo_path}/pulls/${PR_NUMBER}")
    case "$_code" in
        200) return 0 ;;
        404) fail "GitHub pull request #${PR_NUMBER} was not found in ${_repo_path}. Use the number from the pull request URL (…/pull/NN), not the issue list." ;;
        *)   warn "Could not verify PR #${PR_NUMBER} (HTTP ${_code}); continuing with git fetch" ;;
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

    rm -f "${HOME}/.claude/mcp-config-security-review.json" 2>/dev/null || true

    exit "$exit_code"
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# ---------------------------------------------------------------------------
# Parse flags
# ---------------------------------------------------------------------------

COMMENT_FLAG=""
FULL_SCAN=0
for arg in "$@"; do
    case "$arg" in
        --comment)   COMMENT_FLAG="--comment" ;;
        --full-scan) FULL_SCAN=1 ;;
        *)           fail "Unknown argument: ${arg}" ;;
    esac
done

# ---------------------------------------------------------------------------
# Validate required environment variables
# ---------------------------------------------------------------------------

: "${PLATFORM:?PLATFORM is required (github | azure-devops)}"
: "${REPO_URL:?REPO_URL is required — full HTTPS clone URL of the target repo}"

if [ "$FULL_SCAN" = "0" ]; then
    : "${PR_NUMBER:?PR_NUMBER is required (or use --full-scan to scan the whole codebase)}"
    # Ensure PR_NUMBER is a positive integer to guard against injection
    if ! [[ "${PR_NUMBER}" =~ ^[0-9]+$ ]]; then
        fail "PR_NUMBER must be a positive integer, got: '${PR_NUMBER}'"
    fi
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

# Include PID in WORKDIR to guarantee uniqueness for concurrent runs.
if [ "$FULL_SCAN" = "1" ]; then
    WORKDIR="${WORKDIR:-/tmp/security-review-fullscan-$(date +%s)-$$}"
else
    WORKDIR="${WORKDIR:-/tmp/security-review-${PR_NUMBER}-$(date +%s)-$$}"
fi

REPO_SLUG=$(echo "$REPO_URL" \
    | sed 's|https://||; s|\.git$||; s|[/: ]|-|g; s|%[0-9A-Fa-f][0-9A-Fa-f]|-|g')
REPO_CACHE_DIR="${REPO_CACHE_DIR:-/tmp/security-review-cache/${REPO_SLUG}}"
REPO_CACHE_DIR_GLOBAL="${REPO_CACHE_DIR}"

XIANIX_CACHE_DIR="${XIANIX_CACHE_DIR:-/tmp/security-review-cache/xianix-team}"
PLUGIN_DIR="${XIANIX_CACHE_DIR}/plugins/security-agent"

# ---------------------------------------------------------------------------
# Prerequisites check
# ---------------------------------------------------------------------------

for cmd in git claude python3; do
    command -v "$cmd" > /dev/null 2>&1 || fail "'${cmd}' is not installed"
done

if [ "$FULL_SCAN" = "0" ]; then
    post_pr_start_comment
fi

# ---------------------------------------------------------------------------
# Step 1: Build / update the shared bare clone, then create a per-run worktree
# ---------------------------------------------------------------------------

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

log "Creating isolated worktree at ${WORKDIR}"
git -C "${REPO_CACHE_DIR}" worktree add --detach "${WORKDIR}"

cd "${WORKDIR}"

if [ "$FULL_SCAN" = "1" ]; then
    # Full-scan: check out the target branch so we can commit and push back
    SCAN_BRANCH="${SCAN_BRANCH:-main}"
    retry git fetch origin "${SCAN_BRANCH}" --depth="${FETCH_DEPTH}"
    git checkout -B "${SCAN_BRANCH}" FETCH_HEAD
    log "Worktree ready on branch '${SCAN_BRANCH}' for full-scan"
else
    # PR review: fetch and check out the PR branch
    PR_BRANCH="security-review-${PR_NUMBER}-$$"
    case "$PLATFORM" in
        github)
            verify_github_pr_exists
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
fi

# ---------------------------------------------------------------------------
# Step 2: Write MCP config (ephemeral, in home dir, cleaned up on exit)
# ---------------------------------------------------------------------------

log "Writing MCP config"
mkdir -p "${HOME}/.claude"

case "$PLATFORM" in
    github)
        cat > "${HOME}/.claude/mcp-config-security-review.json" <<EOF
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
# Step 3: Clone / update the xianix-team plugin repo
# ---------------------------------------------------------------------------

if [ "${XIANIX_USE_LOCAL:-0}" = "1" ]; then
    log "Using local xianix-team at ${XIANIX_CACHE_DIR} (XIANIX_USE_LOCAL=1)"
elif [ -d "${XIANIX_CACHE_DIR}" ] && git -C "${XIANIX_CACHE_DIR}" rev-parse --git-dir >/dev/null 2>&1; then
    log "Updating xianix-team plugin repo at ${XIANIX_CACHE_DIR}"
    retry git -C "${XIANIX_CACHE_DIR}" pull --ff-only --quiet
else
    if [ -d "${XIANIX_CACHE_DIR}" ]; then
        log "Removing stale xianix-team cache at ${XIANIX_CACHE_DIR} and recloning"
        rm -rf "${XIANIX_CACHE_DIR}"
    fi
    log "Cloning xianix-team plugin repo to ${XIANIX_CACHE_DIR}"
    mkdir -p "$(dirname "${XIANIX_CACHE_DIR}")"
    retry git clone --depth=1 --quiet "${XIANIX_REPO}" "${XIANIX_CACHE_DIR}"
fi

[ -d "${PLUGIN_DIR}" ] || fail "Plugin directory not found at ${PLUGIN_DIR} — check XIANIX_REPO"
log "Plugin ready at ${PLUGIN_DIR}"

# ---------------------------------------------------------------------------
# Step 4: Run the security review
# ---------------------------------------------------------------------------

MCP_CONFIG_ARGS=()
if [ "$PLATFORM" = "github" ]; then
    MCP_CONFIG_ARGS=(--mcp-config "${HOME}/.claude/mcp-config-security-review.json")
fi

if [ "$FULL_SCAN" = "1" ]; then
    SECURITY_REPORT_OUTPUT="${SECURITY_REPORT_OUTPUT:-$(pwd)/SECURITY_REVIEW.md}"
    REVIEW_PROMPT="/security-full-scan ${SECURITY_REPORT_OUTPUT}"
    log "Running full codebase scan — report will be written to ${SECURITY_REPORT_OUTPUT}"
else
    REVIEW_PROMPT="/security-review ${PR_NUMBER} ${COMMENT_FLAG}"
fi

log "Running: ${REVIEW_PROMPT}"

set +e
CLAUDE_OUTPUT=$(claude \
    --dangerously-skip-permissions \
    --verbose \
    --plugin-dir "${PLUGIN_DIR}" \
    ${MCP_CONFIG_ARGS[@]+"${MCP_CONFIG_ARGS[@]}"} \
    -p "${REVIEW_PROMPT}" 2>&1)
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

if [ "$FULL_SCAN" = "1" ]; then
    log "Full-scan complete — report written to ${SECURITY_REPORT_OUTPUT}"

    # ---------------------------------------------------------------------------
    # Step 5: Commit and push the report back to the scanned branch
    # ---------------------------------------------------------------------------

    REPORT_FILE="${SECURITY_REPORT_OUTPUT}"
    [ -f "${REPORT_FILE}" ] || fail "Report file not found at ${REPORT_FILE} — agent may not have written it"

    git config user.name  "${GIT_USER_NAME:?GIT_USER_NAME is required for committing the report (set in .env)}"
    git config user.email "${GIT_USER_EMAIL:?GIT_USER_EMAIL is required for committing the report (set in .env)}"

    # Stage only the report file (relative path inside the worktree)
    REPORT_REL="${REPORT_FILE#${WORKDIR}/}"
    git add "${REPORT_REL}"

    if git diff --cached --quiet; then
        log "Report is unchanged — nothing to commit"
    else
        COMMIT_DATE=$(date -u +"%Y-%m-%d")
        git commit -m "security: automated full-scan report (${COMMIT_DATE})"
        log "Committed report — pushing to origin/${SCAN_BRANCH}"
        retry git push origin "HEAD:${SCAN_BRANCH}"
        log "Report pushed to origin/${SCAN_BRANCH}"
    fi
else
    log "Security review complete"
fi
