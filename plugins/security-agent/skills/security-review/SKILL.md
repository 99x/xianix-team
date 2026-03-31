---
name: security-review
description: Run a security review on a pull request. Scans changed lines for vulnerabilities, secrets, and insecure patterns. Usage: /security-review [pr-number]
argument-hint: [pr-number]
---

Run a security review on pull request $ARGUMENTS.

Use the **security-agent** to:

1. Fetch PR context via MCP:
   - `mcp__github__get_pull_request` — title, description, author, target branch
   - `mcp__github__list_pull_request_files` — changed files with patch content
   - `mcp__github__get_pull_request_diff` — full unified diff

2. Analyze changed lines for:
   - Secrets & credentials (hardcoded keys, tokens, passwords)
   - Injection vulnerabilities (SQL, command, XSS, path traversal, SSRF)
   - Auth & authorization issues (missing checks, broken access control)
   - Cryptography weaknesses (weak algorithms, disabled cert validation)
   - Sensitive data exposure (PII in logs, unencrypted storage)

3. Compile a structured findings report with severity ratings and recommendations

4. Post findings to GitHub automatically — no user confirmation required

5. If invoked with `--comment`: post as a comment instead of a formal PR review

If a PR number is provided (e.g., `/security-review 123`), fetch the PR details first.

If no argument is given, prompt the user for a PR number.
