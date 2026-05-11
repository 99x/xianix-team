---
name: security-review
description: Run a security review on a pull request. Scans changed lines for vulnerabilities, secrets, and insecure patterns. Usage: /security-review [pr-number]
argument-hint: [pr-number]
---

Run a security review on pull request $ARGUMENTS.

## What This Does

This command invokes the **security-agent** which analyzes the PR diff for:

| Category | What It Checks |
|---|---|
| Secrets & Credentials | Hardcoded keys, tokens, passwords, connection strings |
| Injection | SQL injection, command injection, XSS, path traversal, SSRF |
| Auth & Authorization | Missing auth checks, broken access control, insecure tokens |
| Cryptography | Weak algorithms, disabled cert validation, hardcoded IVs |
| Sensitive Data | PII in logs, unencrypted storage, debug output in prod paths |

## How to Use

```
/security-review 123           # Review PR #123, post as formal review
/security-review 123 --comment # Post findings as a comment instead
```

## Output

```
## Security Review
Verdict: APPROVED | APPROVED WITH SUGGESTIONS | CHANGES REQUESTED

### Findings
| # | Severity | Category | File | Line | Issue | Recommendation |

### Summary
```

The review is posted to the PR automatically. The agent outputs:

```
Security review posted on PR #<number>: <verdict> — <N> findings
```

## Prerequisites

- GitHub MCP server must be connected

---

Starting security review now...
