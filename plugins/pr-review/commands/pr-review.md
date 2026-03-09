---
name: pr-review
description: Run a full PR review. Analyzes code quality, security, tests, and performance. Usage: /pr-review [PR number, branch name, or leave blank for current branch]
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
/pr-review 123          # Review GitHub PR #123
/pr-review feature/foo  # Review branch feature/foo vs main
```

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

To post the review to GitHub:
```
/post-review 123
```

## Prerequisites

- Must be run inside a git repository
- For GitHub integration: `gh` CLI installed and authenticated (`gh auth login`)
- For PR number lookup: repo must have a GitHub remote

---

Starting review now...
