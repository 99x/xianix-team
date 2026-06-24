---
name: analyze-impact
description: Analyze a PR for functional impact, blast radius, and QA risk. Produces a prioritized test plan for QA teams. Usage: /impact-analysis [PR number or branch name]
argument-hint: [pr-number or branch-name]
---

Perform a comprehensive impact analysis of the pull request $ARGUMENTS.

Use the **imp-analyst** agent to:

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

3. Run specialized sub-agent analyses in parallel:
   - **change-scope-analyzer** — Categorizes changes: new code, modified logic, deletions, config changes, schema changes
   - **dependency-tracer** — Traces callers, callees, data flows, and transitive dependencies from changed code
   - **feature-mapper** — Maps code paths to user-facing features, routes, API endpoints, business workflows
   - **risk-assessor** — Rates risk per impacted area, assesses regression likelihood, recommends test priorities

4. Compile all findings into a single structured impact report with:
   - Overall risk level: `🔴 HIGH`, `🟡 MEDIUM`, or `🟢 LOW`
   - Executive summary for QA leads
   - High-risk areas with specific test recommendations
   - Impacted features mapped from code to user workflows
   - Blast radius (direct, dependent, indirect)
   - Regression risk assessment
   - Prioritized test plan (P0/P1/P2)

5. Post the report to the detected platform automatically — no user confirmation required:
   - **GitHub**: see `providers/github.md` — posts as PR comment via GitHub MCP or `gh` CLI
   - **Azure DevOps**: see `providers/azure-devops.md` — posts as PR thread via `curl`
   - **Generic / unknown**: see `providers/generic.md` — writes report to `impact-analysis-report.md`

If a branch name is provided (e.g., `/impact-analysis feature/my-feature`), compare that branch against `main`.

If no argument is given, analyze the **current branch** against `main`.
