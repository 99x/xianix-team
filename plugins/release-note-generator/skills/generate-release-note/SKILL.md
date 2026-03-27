---
name: generate-release-note
description: Generate and publish release notes for an Azure DevOps sprint. Fetches work items via WIQL, classifies them, and formats structured Markdown. Usage: /generate-release-note [sprint-name]
argument-hint: "sprint-name"
disable-model-invocation: true
---

Generate release notes for $ARGUMENTS.

Use the **release-note-generator** agent to:

1. Resolve the sprint or iteration reference from $ARGUMENTS.

2. Delegate to **change-analyst** to fetch and classify work items from Azure DevOps:
   - WIQL query by iteration path → batch fetch work items (up to 200 per request)
   - Classify into: Feature, User Story, Bug Fix, Task, Epic

3. Delegate to **content-writer** to produce formatted Markdown using `styles/release-note.md`.

4. Compile the final release note with a verdict:
   - `PUBLISHED` — posted to Azure DevOps wiki successfully
   - `PREVIEW` — generated but not posted (`--preview` flag)
   - `SAVED TO FILE` — written to local file (no `AZURE_DEVOPS_WIKI_URL` set)

5. Post to Azure DevOps wiki via `providers/azure-devops.md`, or fall back to `providers/generic.md` if `AZURE_DEVOPS_WIKI_URL` is not set.

If invoked with `--preview`, generate the Markdown and display it but do not post anywhere.
