---
name: security-full-scan
description: Run a security review across the entire codebase and write findings to a Markdown report. Usage: /security-full-scan [output-path]
argument-hint: "[output-path]"
---

Run a full codebase security scan and write findings to $ARGUMENTS.

## What This Does

This command invokes the **security-agent** to scan every source file in the
working directory for security vulnerabilities and write a structured Markdown
report. Unlike `/security-review`, this is not diff-scoped — it covers the
entire codebase.

| Category | What It Checks |
|---|---|
| Secrets & Credentials | Hardcoded keys, tokens, passwords, connection strings |
| Injection | SQL injection, command injection, XSS, path traversal, SSRF |
| Auth & Authorization | Missing auth checks, broken access control, insecure tokens |
| Cryptography | Weak algorithms, disabled cert validation, hardcoded IVs |
| Sensitive Data | PII in logs, unencrypted storage, debug output in prod paths |
| Supply Chain | New or unpinned dependencies, removed lock files |
| Misconfig | Debug modes, missing headers, overly broad permissions |

## How to Use

```
/security-full-scan                         # Write report to SECURITY_REVIEW.md in current directory
/security-full-scan /path/to/REPORT.md      # Write report to a specific path
```

## Output

A Markdown report is written to the specified path (or `SECURITY_REVIEW.md`):

```
## Security Review — Full Codebase Scan
Date: YYYY-MM-DD
Repository: <remote URL>
Branch: <branch>

### Findings
| # | Severity | Category | File | Line | Issue | Recommendation |

### Summary
```

The agent outputs on completion:

```
Full-scan complete — report written to <path>: <verdict> — <N> findings
```

---

Starting full codebase security scan now...
