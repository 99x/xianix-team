# Provider: Generic / Unknown Platform

Use this provider when the git remote does not match GitHub, Azure DevOps, or Bitbucket — or as a fallback when API posting is not possible.

## Behaviour

In generic mode the impact report is **not posted to a remote platform**. Instead, the compiled report is written to a local file so it can be consumed by an external process, CI system, or human operator.

---

## Writing the Report File

Write the full compiled impact analysis report to a file in the repository root:

```
impact-analysis-report.md
```

The file must be written even if the overall risk is LOW — the file serves as the audit artifact.

**File format:**

```markdown
# Impact Analysis Report

Generated: <ISO 8601 timestamp>
Branch: <current branch>
Base: <base branch>
Commit: <HEAD SHA>
Risk Level: 🔴 HIGH | 🟡 MEDIUM | 🟢 LOW

---

<full compiled impact analysis report body>
```

---

## Output

On completion:

```
Impact analysis complete: <risk-level> — report written to impact-analysis-report.md
```

---

## When to Use

This provider is the correct fallback for:

- Bitbucket (API posting not yet implemented — use generic)
- Self-hosted GitLab instances
- Any on-premises git server
- Local or offline runs where no remote API is available
- CI environments where only the report file output is needed
