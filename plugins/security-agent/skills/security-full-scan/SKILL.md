---
name: security-full-scan
description: Run a security review across the entire codebase and write findings to a Markdown report. Usage: /security-full-scan [output-path]
argument-hint: "[output-path]"
---

Run a full codebase security scan and write findings to $ARGUMENTS.

Use the **security-agent** to:

1. Determine the output path for the Markdown report:
   - If an argument is provided, use it as the absolute or relative path.
   - Otherwise, default to `SECURITY_REVIEW.md` in the current working directory.

2. Enumerate all source files in the current working directory using Bash:
   ```bash
   git ls-files
   ```
   Focus on files with code extensions (`.py`, `.js`, `.ts`, `.go`, `.java`, `.cs`,
   `.rb`, `.php`, `.sh`, `.tf`, `.yaml`, `.yml`, `.json`, `.xml`, `.env*`, `Dockerfile*`).
   Skip binary files, compiled outputs, and lock files.

3. Read and analyze each file for security issues against OWASP Top 10:2025 categories:
   - Secrets & credentials (hardcoded keys, tokens, passwords)
   - Injection vulnerabilities (SQL, command, XSS, path traversal, SSRF)
   - Auth & authorization issues (missing checks, broken access control)
   - Cryptography weaknesses (weak algorithms, disabled cert validation)
   - Sensitive data exposure (PII in logs, unencrypted storage)
   - Supply chain risks (unpinned dependencies, removed lock files)
   - Security misconfigurations (debug modes, missing headers, broad permissions)

4. Compile all findings into a structured report.

5. Detect repository metadata using Bash:
   ```bash
   git remote get-url origin
   git branch --show-current
   date +%Y-%m-%d
   ```

6. Write the Markdown report to the output path using the Write tool. Do not post
   to any PR or external system — the report file is the only output.

7. Print a single completion line:
   ```
   Full-scan complete — report written to <path>: <verdict> — <N> findings (<N> critical, <N> high, <N> medium, <N> low)
   ```

---

## Report Format

The written Markdown file must follow this structure exactly:

```markdown
## Security Review — Full Codebase Scan

**Date:** YYYY-MM-DD
**Repository:** <git remote URL>
**Branch:** <branch name>
**Verdict:** `APPROVED` | `APPROVED WITH SUGGESTIONS` | `CHANGES REQUESTED`

---

### Findings

| # | Severity | Category | File | Line | Issue | Recommendation |
|---|---|---|---|---|---|---|
| 1 | `CRITICAL` / `HIGH` / `MEDIUM` / `LOW` / `INFO` | [category] | [file:line] | [line] | [issue description] | [how to fix] |

*(If no findings: "No security issues found.")*

---

### Summary

[3-5 sentences: overall security posture, most critical findings, and recommended next steps.]
```

---

## Important Guidelines

- **Never include actual sensitive values in the report** — do not reproduce hardcoded secrets, tokens, passwords, keys, or PII verbatim. Refer to the finding by file and line only (e.g., "hardcoded API key at line 42") and redact the value itself.
- Be **specific** about location (file and line) but **never quote the sensitive value itself**.
- If a potential secret looks like a placeholder (e.g., `YOUR_API_KEY_HERE`), mark as INFO only.

---

## Verdict Criteria

| Verdict | Criteria |
|---|---|
| `APPROVED` | No findings, or only INFO-level observations |
| `APPROVED WITH SUGGESTIONS` | MEDIUM or LOW findings only |
| `CHANGES REQUESTED` | Any HIGH or CRITICAL finding |
