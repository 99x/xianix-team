---
name: review-pr
description: Trigger a comprehensive PR review. Runs code quality, security, test coverage, and performance analysis. Usage: /review-pr [PR number or branch name]
argument-hint: [pr-number or branch-name]
---

Perform a comprehensive review of the pull request$ARGUMENTS.

Use the **pr-reviewer** agent to:

1. Gather all changed files and diffs (comparing against `main` or the specified PR's base branch)
2. Run specialized sub-agent reviews in parallel:
   - **code-reviewer** — Code quality, readability, naming, duplication, error handling
   - **security-reviewer** — OWASP vulnerabilities, secrets, injection, auth issues
   - **test-reviewer** — Test coverage, edge cases, test quality
   - **performance-reviewer** — N+1 queries, algorithmic complexity, memory issues
3. Compile all findings into a single structured report with:
   - Overall verdict: `APPROVE`, `REQUEST CHANGES`, or `NEEDS DISCUSSION`
   - Critical issues (must fix before merge)
   - Warnings (should fix)
   - Suggestions (optional improvements)
   - Per-category summaries

If a PR number is provided (e.g., `/review-pr 123`), fetch the PR details via `gh pr view 123` first.

If a branch name is provided (e.g., `/review-pr feature/my-feature`), compare that branch against `main`.

If no argument is given, review the **current branch** against `main`.
