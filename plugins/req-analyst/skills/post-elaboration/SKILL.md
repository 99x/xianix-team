---
name: post-elaboration
description: Post the elaborated requirement to a GitHub issue. Requires an issue number. Usage: /post-elaboration [issue-number]
argument-hint: [issue-number]
---

Post the elaborated requirement to GitHub issue #$ARGUMENTS.

Do not ask for confirmation at any point. Execute all steps autonomously and proceed immediately from one step to the next.

## Steps

1. **Verify issue exists**

   Use `mcp__github__get_issue` with the given issue number to confirm it exists and retrieve its current state, title, and labels. If the issue does not exist or is already closed, stop and output a single error line — do not ask the user what to do.

2. **Format the elaboration for GitHub**

   - Prepare the full elaborated requirement as the issue body (or comment body if `--comment` flag is set)
   - Map verdict to a GitHub label:

     | Plugin verdict | GitHub label |
     |---|---|
     | `GROOMED` | `groomed` |
     | `NEEDS CLARIFICATION` | `needs-clarification` |
     | `NEEDS DECOMPOSITION` | `needs-decomposition` |

3. **Post the elaboration**

   **Default (update issue body):**
   Use `mcp__github__update_issue` with:
   - `issue_number`: the issue number
   - `body`: the full elaborated requirement

   **Comment mode (`--comment`):**
   Use `mcp__github__create_issue_comment` with:
   - `issue_number`: the issue number
   - `body`: the full elaborated requirement

   Then use `mcp__github__add_labels_to_issue` to apply the appropriate status label.

   If unresolved questions exist, post each as a separate comment using `mcp__github__create_issue_comment`, tagging the relevant person (@creator or @product-owner).

4. **Output result**

   On completion, output a single summary line:

   ```
   Elaboration posted on issue #<number>: <verdict> — <N> acceptance criteria — <N> unresolved questions
   ```

   If any MCP call fails, output the error and stop — do not retry or ask for input.

> **Note:** Requires the GitHub MCP server to be connected. See `docs/mcp-config.md` for setup.
