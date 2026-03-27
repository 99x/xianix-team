#!/usr/bin/env bash
# run-release-notes.sh
#
# Bootstrap script for autonomous release note generation.
# Fetches work items from Azure DevOps via WIQL and publishes to the wiki.
# No git clone or worktree is needed — all content comes from the ADO REST API.
#
# Supports: Azure DevOps
#
# Usage:
#   ./scripts/run-release-notes.sh [--preview]
#
# Required environment variables:
#
#   SPRINT_NAME       Sprint or iteration name (e.g. "Sprint 42")
#   AZURE_TOKEN       PAT with Work Items (Read) + Wiki (Read & Write) scopes
#
# At least one of the following must be set so the agent can derive org/project:
#   AZURE_DEVOPS_WIKI_URL  Target wiki page URL (also used for publishing)
#   AZURE_ORG + AZURE_PROJECT  Explicit org and project overrides
#
# Optional:
#   AZURE_DEVOPS_WIKI_URL         Wiki page URL to publish to (omit to write to local file)
#   AZURE_ORG                     Override org (parsed from AZURE_DEVOPS_WIKI_URL if not set)
#   AZURE_PROJECT                 Override project (parsed from AZURE_DEVOPS_WIKI_URL if not set)
#   AZURE_DEVOPS_WORK_ITEM_TYPES  Comma-separated work item types (default: User Story,Bug,Feature,Task,Epic)
#   AZURE_DEVOPS_ITERATION_PATH_PREFIX  Iteration path prefix (default: AZURE_PROJECT)
#   XIANIX_REPO           Xianix plugin marketplace repo (default: https://github.com/99x/xianix-team.git)
#   XIANIX_CACHE_DIR      Local path for the cloned xianix-team repo (default: /tmp/release-notes-cache/xianix-team)
#   RELEASE_PREVIEW_ONLY  Set to "1" to generate without publishing
#   KEEP_WORKDIR          (no-op — kept for script compatibility)
#   GIT_RETRY_COUNT       Number of retries for network operations (default: 3)
#   GIT_RETRY_DELAY       Seconds between retries (default: 5)

set -euo pipefail

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

readonly SCRIPT_NAME="run-release-notes"
readonly RETRY_COUNT="${GIT_RETRY_COUNT:-3}"
readonly RETRY_DELAY="${GIT_RETRY_DELAY:-5}"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

log()  { echo "[${SCRIPT_NAME}] $*"; }
warn() { echo "[${SCRIPT_NAME}] WARN: $*" >&2; }

LAST_ERROR_MESSAGE=""

fail() {
    echo "[${SCRIPT_NAME}] ERROR: $*" >&2
    LAST_ERROR_MESSAGE="$*"
    exit 1
}

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
# Cleanup trap
# ---------------------------------------------------------------------------

cleanup() {
    local exit_code=$?
    rm -f "${HOME}/.claude/mcp-config-release-notes.json" 2>/dev/null || true
    exit "$exit_code"
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# ---------------------------------------------------------------------------
# Parse flags
# ---------------------------------------------------------------------------

PREVIEW_FLAG=""
for arg in "$@"; do
    case "$arg" in
        --preview) PREVIEW_FLAG="--preview" ;;
        *)         fail "Unknown argument: ${arg}" ;;
    esac
done

if [ "${RELEASE_PREVIEW_ONLY:-}" = "1" ]; then
    PREVIEW_FLAG="--preview"
fi

# ---------------------------------------------------------------------------
# Validate required environment variables
# ---------------------------------------------------------------------------

: "${SPRINT_NAME:?SPRINT_NAME is required (e.g. 'Sprint 42')}"
: "${AZURE_TOKEN:?AZURE_TOKEN is required — PAT with Work Items Read + Wiki Read & Write}"

# Ensure we can derive org and project (needed by the agent)
if [ -z "${AZURE_ORG:-}" ] && [ -z "${AZURE_DEVOPS_WIKI_URL:-}" ]; then
    fail "Either AZURE_ORG + AZURE_PROJECT or AZURE_DEVOPS_WIKI_URL must be set so the agent can determine the Azure DevOps org and project."
fi

# ---------------------------------------------------------------------------
# Derive directory paths
# ---------------------------------------------------------------------------

XIANIX_REPO="${XIANIX_REPO:-https://github.com/99x/xianix-team.git}"
XIANIX_CACHE_DIR="${XIANIX_CACHE_DIR:-/tmp/release-notes-cache/xianix-team}"
PLUGIN_DIR="${XIANIX_CACHE_DIR}/plugins/release-note-generator"

# ---------------------------------------------------------------------------
# Prerequisites check
# ---------------------------------------------------------------------------

for cmd in claude python3 curl; do
    command -v "$cmd" > /dev/null 2>&1 || fail "'${cmd}' is not installed"
done

# ---------------------------------------------------------------------------
# Step 1: Clone / update the xianix-team plugin repo
# ---------------------------------------------------------------------------

if [ "${XIANIX_USE_LOCAL:-0}" = "1" ]; then
    log "Using local xianix-team at ${XIANIX_CACHE_DIR} (XIANIX_USE_LOCAL=1)"
elif [ -d "${XIANIX_CACHE_DIR}" ] && git -C "${XIANIX_CACHE_DIR}" rev-parse --git-dir >/dev/null 2>&1; then
    log "Updating xianix-team plugin repo at ${XIANIX_CACHE_DIR}"
    retry git -C "${XIANIX_CACHE_DIR}" pull --ff-only --quiet
else
    if [ -d "${XIANIX_CACHE_DIR}" ]; then
        log "Removing stale xianix-team cache at ${XIANIX_CACHE_DIR}"
        rm -rf "${XIANIX_CACHE_DIR}"
    fi
    log "Cloning xianix-team plugin repo to ${XIANIX_CACHE_DIR}"
    mkdir -p "$(dirname "${XIANIX_CACHE_DIR}")"
    retry git clone --depth=1 --quiet "${XIANIX_REPO}" "${XIANIX_CACHE_DIR}"
fi

[ -d "${PLUGIN_DIR}" ] || fail "Plugin directory not found at ${PLUGIN_DIR} — check XIANIX_REPO"
log "Plugin ready at ${PLUGIN_DIR}"

# ---------------------------------------------------------------------------
# Step 2: Run the release note generation
# ---------------------------------------------------------------------------

RELEASE_PROMPT="/generate-release-note ${SPRINT_NAME} ${PREVIEW_FLAG}"
log "Running: ${RELEASE_PROMPT}"

set +e
CLAUDE_OUTPUT=$(claude \
    --dangerously-skip-permissions \
    --verbose \
    --plugin-dir "${PLUGIN_DIR}" \
    -p "${RELEASE_PROMPT}" 2>&1)
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

log "Release note generation complete"
