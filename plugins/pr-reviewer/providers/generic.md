# Provider: Generic / Unknown Platform

Use this provider when the git remote does not match GitHub, Azure DevOps, or Bitbucket — or as a fallback when API posting is not possible.

## Behaviour

In generic mode the review is **not posted to a remote platform**. Instead, the compiled report is written to a local file so it can be consumed by an external process, CI system, or human operator.

---

## Writing the Report File

Write the full compiled review report to a file in the repository root:

```
pr-review-report.md
```

The file must be written even if the review verdict is `APPROVE` — the file serves as the audit artifact.

**File format:**

```markdown
# PR Review Report

Generated: <ISO 8601 timestamp>
Branch: <current branch>
Base: <base branch>
Commit: <HEAD SHA>
Verdict: APPROVE | REQUEST CHANGES | NEEDS DISCUSSION

---

<full compiled review report body>
```

---

## Fix Mode

Fix mode operates identically to GitHub and Azure DevOps providers — apply fixes, commit, and push. The post-fix summary is appended to `pr-review-report.md` rather than posted as a comment.

---

## Output

On completion:

```
Review complete: <verdict> — report written to pr-review-report.md
```

---

## When to Use

This provider is the correct fallback for:

- Bitbucket (API posting not yet implemented — use generic)
- Self-hosted GitLab instances
- Any on-premises git server
- Local or offline runs where no remote API is available
- CI environments where only the report file output is needed
