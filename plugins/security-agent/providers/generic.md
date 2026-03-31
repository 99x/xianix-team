# Provider: Generic / Unknown Platform

Use this provider when the git remote does not match GitHub or Azure DevOps — or as a fallback when API posting is not possible.

## Behaviour

In generic mode the security report is **not posted to a remote platform**. Instead, the report is written to a local file so it can be consumed by a CI system, pipeline, or human operator.

---

## Writing the Report File

Write the full compiled security report to a file in the repository root:

```
security-review-report.md
```

The file must be written regardless of verdict — it serves as the audit artifact.

**File format:**

```markdown
# Security Review Report

Generated: <ISO 8601 timestamp>
Repository: <repo URL>
PR: #<pr number>
Verdict: APPROVED | APPROVED WITH SUGGESTIONS | CHANGES REQUESTED

---

<full findings table and summary>
```

---

## Output

On completion:

```
Security review complete: <verdict> — report written to security-review-report.md
```

---

## When to Use

- Bitbucket, GitLab, or other platforms not yet directly supported
- Self-hosted git servers
- Local or offline runs where no remote API is available
- CI environments where only the report file output is needed
