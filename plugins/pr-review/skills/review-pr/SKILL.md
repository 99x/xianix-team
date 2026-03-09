---
name: review-pr
description: Trigger a comprehensive PR review. Runs code quality, security, test coverage, and performance analysis. Usage: /review-pr [PR number or branch name]
argument-hint: [pr-number or branch-name]
---

Perform a comprehensive review of the pull request $ARGUMENTS.

Use the **pr-reviewer** agent to:

1. Fetch PR context via MCP (always fresh — no local git diff needed):
   - `mcp__github__get_pull_request` — title, author, base branch, stats
   - `mcp__github__list_pull_request_files` — changed files with patches
   - `mcp__github__get_file_contents` — full file content where needed

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

4. Post the review to GitHub automatically — no user confirmation required:
   - Use `mcp__github__create_pull_request_review` for the overall verdict and report body
   - Use `mcp__github__add_pull_request_review_comment` for each inline finding with a precise file and line

5. If invoked with `--fix`: apply fixes and push before posting:
   - Auto-fix CRITICAL and WARNING issues using `Write` + `git commit` + `git push`
   - Post a follow-up comment listing what was auto-fixed vs what needs manual attention

If a PR number is provided (e.g., `/review-pr 123`), fetch the PR details via `mcp__github__get_pull_request` first.

If a branch name is provided (e.g., `/review-pr feature/my-feature`), compare that branch against `main`.

If no argument is given, review the **current branch** against `main`.
