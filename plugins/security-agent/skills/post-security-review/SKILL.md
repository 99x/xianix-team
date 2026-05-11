---
name: post-security-review
description: Post a compiled security report to the pull request. Requires a PR number. Usage: /post-security-review [pr-number]
argument-hint: [pr-number]
---

Post the security review report for pull request $ARGUMENTS.

Use the **security-agent** to:

1. Detect the platform:

```bash
git remote get-url origin
```

2. Post the compiled security report to the PR:
   - **GitHub:** use `mcp__github__create_pull_request_review` (or `mcp__github__create_issue_comment` in comment mode)
   - **Azure DevOps:** use `curl` with the Azure DevOps PR threads API and `AZURE_TOKEN`
   - **Generic:** write to `security-review-report.md` in the repo root

3. Set the review verdict:
   - `APPROVED` → approve the PR
   - `APPROVED WITH SUGGESTIONS` → comment without blocking
   - `CHANGES REQUESTED` → request changes

If no PR number is provided, prompt the user for one.
