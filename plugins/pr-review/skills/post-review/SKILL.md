---
name: post-review
description: Post the current PR review as GitHub comments. Requires a PR number. Usage: /post-review [pr-number]
argument-hint: [pr-number]
---

Post the PR review findings as GitHub review comments on PR #$ARGUMENTS.

Do not ask for confirmation at any point. Execute all steps autonomously and proceed immediately from one step to the next.

## Steps

1. **Verify PR exists**

   Use `mcp__github__get_pull_request` with the given PR number to confirm it exists and retrieve its current state, title, and head branch. If the PR does not exist or is already merged/closed, stop and output a single error line — do not ask the user what to do.

2. **Format the review for GitHub**

   - Map file-level findings to inline comments — each needs a `path`, `line`, and `body`
   - Prepare the overall review body with the full summary and verdict
   - Map verdict to GitHub's event type:

     | Plugin verdict | GitHub event |
     |---|---|
     | `APPROVE` | `APPROVE` |
     | `REQUEST CHANGES` | `REQUEST_CHANGES` |
     | `NEEDS DISCUSSION` | `COMMENT` |

3. **Post the review**

   Use `mcp__github__create_pull_request_review` with:
   - `pull_number`: the PR number
   - `event`: the mapped GitHub event type
   - `body`: the full compiled review report

   Then for each finding that has a precise file path and line number, use `mcp__github__add_pull_request_review_comment` with:
   - `pull_number`: the PR number
   - `path`: relative file path (e.g. `src/auth/login.ts`)
   - `line`: the line number
   - `body`: the finding description and fix

   Post all inline comments without pausing between them.

4. **Output result**

   On completion, output a single summary line:

   ```
   Posted review on PR #<number>: <verdict> — <N> inline comments — <review URL>
   ```

   If any MCP call fails, output the error and stop — do not retry or ask for input.

> **Note:** Requires the GitHub MCP server to be connected. See `docs/mcp-config.md` for setup.
