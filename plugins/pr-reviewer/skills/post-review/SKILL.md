---
name: post-review
description: Post the current PR review findings as comments on a pull request. Requires a PR number. Usage: /post-review [pr-number]
argument-hint: [pr-number]
disable-model-invocation: true
---

Post the PR review findings as review comments on PR #$ARGUMENTS.

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

   Use the platform-appropriate method to confirm the PR exists and retrieve its current state:

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

3. **Format the review**

   Map the verdict to the platform event type:

   | Plugin verdict | GitHub event | Azure DevOps vote |
   |---|---|---|
   | `APPROVE` | `APPROVE` | `10` |
   | `REQUEST CHANGES` | `REQUEST_CHANGES` | `-10` |
   | `NEEDS DISCUSSION` | `COMMENT` | `0` |

4. **Post the review**

   Follow the instructions in the appropriate provider file:

   - **GitHub** → `providers/github.md`
   - **Azure DevOps** → `providers/azure-devops.md`
   - **Generic / unknown** → `providers/generic.md`

5. **Output result**

   On completion, output a single summary line:

   **GitHub:**
   ```
   Posted review on PR #<number>: <verdict> — <N> inline comments — <review URL>
   ```

   **Azure DevOps:**
   ```
   Posted review on PR #<number>: <verdict> — <N> inline comments — https://dev.azure.com/<org>/<project>/_git/<repo>/pullrequest/<number>
   ```

   **Generic:**
   ```
   Review complete: <verdict> — report written to pr-review-report.md
   ```

   If any step fails, output the error and stop — do not retry or ask for input.

> **Note:** GitHub posting requires the GitHub MCP server to be connected, or the `gh` CLI to be installed. Azure DevOps posting uses `curl` with the `AZURE_TOKEN` environment variable (PAT with Pull Request Threads Read & Write scope). See `docs/platform-setup.md` for setup instructions.
