# Provider: GitHub

Use this provider when `git remote get-url origin` contains `github.com`.

## Prerequisites

The GitHub MCP server must be connected. Run `/mcp` to verify — `github` should show as `connected`. If not connected, see `docs/mcp-config.md`.

Alternatively, the `gh` CLI can be used as a fallback if MCP is unavailable.

---

## Posting the Starting Comment

Before running any analysis, post a plain PR comment to inform the author that a review is underway. This fires as the very first action on GitHub, before sub-agents are launched.

Use `mcp__github__add_issue_comment` with:
- `owner`: repo owner parsed from the remote URL (e.g. `my-org`)
- `repo`: repo name parsed from the remote URL (e.g. `my-repo`)
- `issue_number`: the PR number (PRs share the same number space as issues on GitHub)
- `body`:

```
🔍 **PR review in progress**

I'm running a comprehensive review covering code quality, security, test coverage, and performance. The full results will be posted as a review comment when complete — this may take a few minutes.
```

Parse `owner` and `repo` from the remote URL:

```bash
REMOTE=$(git remote get-url origin)
# https://github.com/org/repo.git  →  owner=org  repo=repo
# git@github.com:org/repo.git      →  owner=org  repo=repo
OWNER=$(echo "$REMOTE" | sed 's|https://github.com/||;s|git@github.com:||' | cut -d'/' -f1)
REPO=$(echo "$REMOTE"  | sed 's|https://github.com/||;s|git@github.com:||' | cut -d'/' -f2 | sed 's|\.git$||')
```

If posting the starting comment fails, output a single warning line and continue — do not stop the review.

---

## Posting the Review

### Option A — GitHub MCP (preferred)

**Post the overall verdict and report body:**

Use `mcp__github__create_pull_request_review` with:
- `pull_number`: the PR number (parsed from `gh pr list --head <branch>` or passed in as an argument)
- `event`: mapped from the verdict:

  | Plugin verdict | GitHub event |
  |---|---|
  | `APPROVE` | `APPROVE` |
  | `REQUEST CHANGES` | `REQUEST_CHANGES` |
  | `NEEDS DISCUSSION` | `COMMENT` |

- `body`: the full compiled review report

**Post inline comments:**

For each finding that has a precise file path and line number, use `mcp__github__add_pull_request_review_comment` with:
- `pull_number`: the PR number
- `path`: relative file path (e.g. `src/auth/login.ts`)
- `line`: the line number
- `body`: the finding description and fix

Post all inline comments without pausing between them.

### Option B — `gh` CLI (fallback if MCP is unavailable)

**Find the PR number for the current branch:**

```bash
gh pr list --head $(git rev-parse --abbrev-ref HEAD) --json number --jq '.[0].number'
```

**Post the overall review:**

```bash
gh pr review <pr-number> --approve --body "<report>"
# or
gh pr review <pr-number> --request-changes --body "<report>"
# or
gh pr review <pr-number> --comment --body "<report>"
```

**Post inline comments (one per finding):**

```bash
gh api repos/{owner}/{repo}/pulls/<pr-number>/comments \
  --method POST \
  --field path="src/auth/login.ts" \
  --field line=42 \
  --field side="RIGHT" \
  --field body="Finding description and fix" \
  --field commit_id="$(git rev-parse HEAD)"
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
Review posted on PR #<number>: <verdict> — <N> inline comments — https://github.com/<owner>/<repo>/pull/<number>
```
