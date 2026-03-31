# Provider: GitHub

Use this provider when `git remote get-url origin` contains `github.com`.

## Prerequisites

The GitHub MCP server must be connected. Run `/mcp` to verify — `github` should show as `connected`.

---

## Posting the Security Review

### Option A — GitHub MCP (preferred)

**Post a formal PR review:**

Use `mcp__github__create_pull_request_review` with:
- `pull_number`: the PR number
- `event`: one of `APPROVE`, `COMMENT`, or `REQUEST_CHANGES` based on verdict:

  | Verdict | GitHub event |
  |---|---|
  | `APPROVED` | `APPROVE` |
  | `APPROVED WITH SUGGESTIONS` | `COMMENT` |
  | `CHANGES REQUESTED` | `REQUEST_CHANGES` |

- `body`: the full security report (findings table + summary)

**Comment mode (`--comment`):** Use `mcp__github__create_issue_comment` instead of a formal review.

### Option B — `gh` CLI (fallback if MCP is unavailable)

**Post a review:**

```bash
gh pr review <pr-number> --approve --body "<report>"
gh pr review <pr-number> --comment --body "<report>"
gh pr review <pr-number> --request-changes --body "<report>"
```

---

## Resolving the PR

If no PR number was passed as an argument:

```bash
git remote get-url origin
# e.g. https://github.com/org/repo.git → owner=org, repo=repo

gh pr list --limit 10 --json number,title,headRefName
```

---

## Output

On completion:

```
Security review posted on PR #<number>: <verdict> — <N> findings (<N> critical, <N> high, <N> medium, <N> low)
```
