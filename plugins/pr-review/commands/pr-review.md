---
name: pr-review
description: Run a full PR review. Analyzes code quality, security, tests, and performance. Works with GitHub, Azure DevOps, Bitbucket, and any git repository. Usage: /pr-review [PR number, branch name, or leave blank for current branch]
argument-hint: [pr-number | branch-name]
---

Run a comprehensive pull request review for $ARGUMENTS.

## What This Does

This command invokes the **pr-reviewer** agent which orchestrates four specialized reviewers:

| Reviewer | Focus |
|----------|-------|
| `code-reviewer` | Readability, naming, duplication, error handling, design patterns |
| `security-reviewer` | OWASP Top 10, secrets, injection, auth/authz vulnerabilities |
| `test-reviewer` | Coverage gaps, test quality, edge cases, missing regression tests |
| `performance-reviewer` | N+1 queries, O(n²) loops, memory leaks, blocking I/O |

## How to Use

```
/pr-review              # Review current branch vs main
/pr-review 123          # Review PR #123 (GitHub) or PR ID 123 (Azure DevOps)
/pr-review feature/foo  # Review branch feature/foo vs main
/pr-review 123 --fix    # Review and auto-apply fixes
```

## Platform Support

The plugin auto-detects the hosting platform from your git remote URL:

| Remote URL contains | Platform | How review is posted |
|---|---|---|
| `github.com` | GitHub | GitHub MCP server or `gh` CLI |
| `dev.azure.com` / `visualstudio.com` | Azure DevOps | `az repos pr` CLI |
| Anything else | Generic | Written to `pr-review-report.md` |

All diff and file content gathering uses standard git commands — no platform-specific API is needed for the analysis phase.

## Output

The review produces a structured report:

```
## PR Review Report
Verdict: APPROVE | REQUEST CHANGES | NEEDS DISCUSSION

### Critical Issues (Must Fix)
### Warnings (Should Fix)
### Suggestions (Consider Improving)
### Code Quality
### Security
### Test Coverage
### Performance
### Files Reviewed
```

## After the Review

The review is posted to your platform automatically as part of this command — no further steps required. The agent will output a single confirmation line:

**GitHub:**
```
Review posted on PR #<number>: <verdict> — <N> inline comments — <URL>
```

**Azure DevOps:**
```
Review posted on PR #<number>: <verdict> — <N> inline comments — <URL>
```

**Generic / unknown platform:**
```
Review complete: <verdict> — report written to pr-review-report.md
```

## Prerequisites

- Must be run inside a git repository
- The current branch must have at least one commit ahead of the base branch
- **GitHub**: GitHub MCP server connected (see `docs/platform-setup.md`) or `gh` CLI installed
- **Azure DevOps**: `az` CLI installed with `azure-devops` extension and authenticated (see `docs/platform-setup.md`)
- **Fix mode**: `GIT_TOKEN` (GitHub) or `AZURE_DEVOPS_PAT` (Azure DevOps) must be set for `git push`

---

Starting review now...
