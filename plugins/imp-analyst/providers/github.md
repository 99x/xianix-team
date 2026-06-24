# Provider: GitHub

Use this provider when `git remote get-url origin` contains `github.com`.

## Prerequisites

The GitHub MCP server must be connected. Run `/mcp` to verify — `github` should show as `connected`. If not connected, see `docs/mcp-config.md`.

Alternatively, the `gh` CLI can be used as a fallback if MCP is unavailable.

---

## Posting the Impact Report

The impact analysis report is posted as a **PR comment** (not a review), since it is informational for QA rather than a code review verdict. If the PR body links to a GitHub issue, a condensed summary is also posted to that issue.

### Option A — GitHub MCP (preferred)

**Step 1 — Post the full report as a PR comment:**

Use `mcp__github__create_issue_comment` with:
- `issue_number`: the PR number (GitHub PRs are issues)
- `body`: the full compiled impact analysis report

Capture the URL of the posted comment for use in the issue summary.

**Step 2 — Post a condensed summary to the linked issue (if any):**

1. Fetch the PR body using `mcp__github__get_pull_request` (you likely already have this from Step 1 of the orchestrator).
2. Scan the PR body for closing keywords followed by an issue number:
   - Pattern: `(?:closes?|fixes?|resolves?)\s+#(\d+)` (case-insensitive)
   - Also match cross-repo references: `org/repo#123` — skip these (only post to same-repo issues)
3. For each matched issue number, post a condensed comment using `mcp__github__create_issue_comment`:
   - `issue_number`: the linked issue number
   - `body`: the condensed summary (see format below)

**Condensed issue comment format:**

```
## Impact Analysis — PR #<pr-number>

**Overall Risk:** `🔴 HIGH` | `🟡 MEDIUM` | `🟢 LOW`

**Executive Summary**
<2-3 sentence summary from the full report>

**High-Risk Areas:** <N> area(s) identified

→ [View full impact analysis on PR #<pr-number>](<pr-comment-url>)
```

### Option B — `gh` CLI (fallback if MCP is unavailable)

**Find the PR number for the current branch:**

```bash
gh pr list --head $(git rev-parse --abbrev-ref HEAD) --json number --jq '.[0].number'
```

**Post the full report as a PR comment:**

```bash
gh pr comment <pr-number> --body "<report>"
```

**Extract linked issue numbers from the PR body and post the condensed summary:**

```bash
# Get PR body
PR_BODY=$(gh pr view <pr-number> --json body --jq '.body')

# Extract linked issue numbers (closes/fixes/resolves #N)
ISSUE_NUMBERS=$(echo "$PR_BODY" | grep -ioP '(?:closes?|fixes?|resolves?)\s+#\K\d+')

# Post condensed summary to each linked issue
for ISSUE in $ISSUE_NUMBERS; do
  gh issue comment "$ISSUE" --body "<condensed-summary>"
done
```

---

## Resolving the PR Number

If no PR number was passed as an argument:

1. Get the current branch: `git rev-parse --abbrev-ref HEAD`
2. Parse the GitHub remote to get `{owner}` and `{repo}`:

```bash
git remote get-url origin
# e.g. https://github.com/org/repo.git  →  owner=org, repo=repo
# e.g. git@github.com:org/repo.git      →  owner=org, repo=repo
```

3. Find the PR: `gh pr list --head <branch> --json number --jq '.[0].number'`

---

## Output

On completion:

```
Impact analysis posted on PR #<number>: <risk-level> — <N> high-risk areas — https://github.com/<owner>/<repo>/pull/<number>
```

If a condensed summary was also posted to a linked issue:

```
Impact analysis summary posted on issue #<issue-number>: https://github.com/<owner>/<repo>/issues/<issue-number>
```
