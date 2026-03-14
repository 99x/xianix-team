---
name: review-pr
description: Trigger a comprehensive PR review. Runs code quality, security, test coverage, and performance analysis. Usage: /review-pr [PR number or branch name]
argument-hint: [pr-number or branch-name]
disable-model-invocation: true
---

Perform a comprehensive review of the pull request $ARGUMENTS.

Use the **pr-reviewer** agent to:

1. Detect the hosting platform from the git remote URL:
   ```bash
   git remote get-url origin
   ```
   - `github.com` → GitHub
   - `dev.azure.com` / `visualstudio.com` → Azure DevOps
   - Anything else → Generic (report written to file)

2. Gather PR context via git commands (works on any platform):
   ```bash
   # Base branch
   BASE=$(git symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's|refs/remotes/origin/||' || echo "main")

   git log --oneline origin/${BASE}..HEAD          # commit list
   git diff origin/${BASE}...HEAD                  # full diff with patches
   git diff --name-only origin/${BASE}...HEAD      # changed file names
   git diff --stat origin/${BASE}...HEAD           # change stats
   git rev-parse HEAD                              # head SHA
   git log -1 --format="%an <%ae>"                # author
   git log --format="%s%n%b" origin/${BASE}..HEAD  # PR description from commits
   ```

   Use `Read` or `git show HEAD:<filepath>` to read full file content where needed.

3. Run specialized sub-agent reviews in parallel:
   - **code-reviewer** — Code quality, readability, naming, duplication, error handling
   - **security-reviewer** — OWASP vulnerabilities, secrets, injection, auth issues
   - **test-reviewer** — Test coverage, edge cases, test quality
   - **performance-reviewer** — N+1 queries, algorithmic complexity, memory issues

4. Compile all findings into a single structured report with:
   - Overall verdict: `APPROVE`, `REQUEST CHANGES`, or `NEEDS DISCUSSION`
   - Critical issues (must fix before merge)
   - Warnings (should fix)
   - Suggestions (optional improvements)
   - Per-category summaries

5. Post the review to the detected platform automatically — no user confirmation required:
   - **GitHub**: see `providers/github.md` — uses GitHub MCP or `gh` CLI
   - **Azure DevOps**: see `providers/azure-devops.md` — uses `curl` with `AZURE_TOKEN` environment variable
   - **Generic / unknown**: see `providers/generic.md` — writes report to `pr-review-report.md`

6. If invoked with `--fix`: apply fixes and push before posting:
   - Auto-fix CRITICAL and WARNING issues using `Write` + `git commit` + `git push`
   - Post a follow-up comment listing what was auto-fixed vs what needs manual attention

If a branch name is provided (e.g., `/review-pr feature/my-feature`), compare that branch against `main`.

If no argument is given, review the **current branch** against `main`.
