---
name: post-release-note
description: Post previously generated release notes to Azure DevOps wiki. Requires the release note content to already be available in context. Usage: /post-release-note [sprint-name]
argument-hint: "sprint-name"
disable-model-invocation: true
---

Post the release notes for $ARGUMENTS to Azure DevOps wiki.

Do not ask for confirmation at any point. Execute all steps autonomously and proceed immediately from one step to the next.

## Steps

1. **Verify release note content is available**

   Confirm the generated Markdown from `change-analyst` and `content-writer` is in context. If not, run the full generation flow via `generate-release-note` first.

2. **Post to platform**

   Follow the instructions in the appropriate provider file:

   - If `AZURE_DEVOPS_WIKI_URL` is set → `providers/azure-devops.md`
   - Otherwise → `providers/generic.md`

3. **Output result**

   On completion, output a single summary line:

   **Azure DevOps wiki:**
   ```
   Release notes published: <sprint-name> — <N> items — https://dev.azure.com/<org>/<project>/_wiki/...
   ```

   **Generic (no wiki URL):**
   ```
   Release notes complete: <sprint-name> — <N> items — written to release-notes-<name>.md
   ```

   If any step fails, output the error and stop — do not retry or ask for input.

> **Note:** Posting requires `AZURE_TOKEN` (PAT with Work Items Read and Wiki Read & Write scopes) and `AZURE_DEVOPS_WIKI_URL`. See `docs/platform-setup.md` for setup instructions.
