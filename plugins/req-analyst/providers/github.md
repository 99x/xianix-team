# Provider: GitHub

Use this provider when `git remote get-url origin` contains `github.com`.

## Prerequisites

The GitHub MCP server must be connected. Run `/mcp` to verify — `github` should show as `connected`. If not connected, see `docs/mcp-config.md`.

---

## Posting the Elaboration

### Option A — GitHub MCP (preferred)

**Update the issue body with the elaborated requirement:**

Use `mcp__github__update_issue` with:
- `issue_number`: the issue number
- `body`: the full compiled elaboration report

**Apply verdict label:**

Use `mcp__github__add_labels_to_issue` with:
- `issue_number`: the issue number
- `labels`: mapped from the verdict:

  | Plugin verdict | GitHub label |
  |---|---|
  | `GROOMED` | `groomed` |
  | `NEEDS CLARIFICATION` | `needs-clarification` |
  | `NEEDS DECOMPOSITION` | `needs-decomposition` |

**Post unresolved questions as comments:**

For each unresolved question, use `mcp__github__create_issue_comment` with:
- `issue_number`: the issue number
- `body`: the question text, tagging the relevant person with `@`

### Option B — `gh` CLI (fallback if MCP is unavailable)

**Update the issue body:**

```bash
gh issue edit <issue-number> --body "<elaboration>"
```

**Add labels:**

```bash
gh issue edit <issue-number> --add-label "groomed"
```

**Post comments:**

```bash
gh issue comment <issue-number> --body "<question>"
```

---

## Resolving the Issue

If no issue number was passed as an argument:

1. Parse the GitHub remote to get `{owner}` and `{repo}`:

```bash
git remote get-url origin
# e.g. https://github.com/org/repo.git  →  owner=org, repo=repo
```

2. List recent issues: `gh issue list --limit 10 --json number,title`

---

## Output

On completion:

```
Elaboration posted on issue #<number>: <verdict> — <N> acceptance criteria — <N> unresolved questions
```
