---
name: post-impact-report
description: Post the current impact analysis findings as a comment on a pull request. Requires a PR number. Usage: /post-impact-report [pr-number]
argument-hint: [pr-number]
---

Post the impact analysis findings as a comment on PR #$ARGUMENTS.

Do not ask for confirmation at any point. Execute all steps autonomously and proceed immediately from one step to the next.

## Steps

1. **Detect Platform**

   Run:
   ```bash
   git remote get-url origin
   ```

   Determine the platform:
   - Contains `github.com` → **GitHub**
   - Contains `dev.azure.com` or `visualstudio.com` → **Azure DevOps**
   - Anything else → **Generic**

2. **Verify PR exists**

   **GitHub (MCP):**
   Use `mcp__github__get_pull_request` with the given PR number. If the PR does not exist or is already merged/closed, stop and output a single error line.

   **GitHub (CLI fallback):**
   ```bash
   gh pr view <pr-number> --json state,title,headRefName
   ```

   **Azure DevOps:**
   ```bash
   curl -s -u ":${AZURE_TOKEN}" \
     "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/git/repositories/${AZURE_REPO}/pullrequests/${PR_NUMBER}?api-version=7.1"
   ```
   Parse org, project, repo from `git remote get-url origin` as described in `providers/azure-devops.md`.

   If the PR does not exist or is already completed/abandoned, stop and output a single error line — do not ask the user what to do.

3. **Post the report**

   Follow the instructions in the appropriate provider file:

   - **GitHub** → `providers/github.md`
   - **Azure DevOps** → `providers/azure-devops.md`
   - **Generic / unknown** → `providers/generic.md`

4. **Output result**

   On completion, output a single summary line:

   **GitHub:**
   ```
   Impact analysis posted on PR #<number>: <risk-level> — <N> high-risk areas — <URL>
   ```

   **Azure DevOps:**
   ```
   Impact analysis posted on PR #<number>: <risk-level> — <N> high-risk areas — https://dev.azure.com/<org>/<project>/_git/<repo>/pullrequest/<number>
   ```

   **Generic:**
   ```
   Impact analysis complete: <risk-level> — report written to impact-analysis-report.md
   ```

   If any step fails, output the error and stop — do not retry or ask for input.

> **Note:** GitHub posting requires the GitHub MCP server to be connected, or the `gh` CLI to be installed. Azure DevOps posting uses `curl` with the `AZURE_TOKEN` environment variable (PAT with Pull Request Threads Read & Write scope). See `docs/platform-setup.md` for setup instructions.
