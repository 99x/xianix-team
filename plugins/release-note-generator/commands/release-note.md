---
name: release-note
description: Generate and publish release notes for an Azure DevOps sprint or iteration. Fetches work items via WIQL, classifies them by type, and publishes to the Azure DevOps wiki or a local file. Usage: /release-note [sprint-name]
argument-hint: "sprint-name"
---

Generate release notes for $ARGUMENTS.

## What This Does

This command invokes the **release-note-generator** agent which orchestrates two specialist agents:

| Agent | Focus |
|-------|-------|
| `change-analyst` | Fetches work items from Azure DevOps via WIQL batch API and classifies them by type |
| `content-writer` | Formats the classified items into structured, user-facing release notes Markdown |

## How to Use

```
/release-note "Sprint 42"               # Generate and publish release notes for Sprint 42
/release-note "Sprint 42" --preview     # Preview only — do not publish
```

## Platform Support

The plugin uses Azure DevOps REST API directly via `curl`.

| AZURE_TOKEN | AZURE_DEVOPS_WIKI_URL | Behaviour |
|---|---|---|
| Set | Set | Fetches work items and publishes to wiki |
| Set | Not set | Fetches work items and writes to local file |
| Not set | — | Blocked by validate-prerequisites hook |

## Output

The release notes produce a structured document:

```
## Release Notes — Sprint 42

| Type | Count |
|---|---|
| 🚀 Features | 3 |
| 📖 User Stories | 8 |
| 🐛 Bug Fixes | 5 |
| ✅ Tasks | 4 |

### 🚀 Features
| ID | Title | State | Assigned To |
|---|---|---|---|
| #1234 | ... | Done | Alice |
...
```

## After Generation

The release notes are published automatically — no further steps required. The agent outputs a single confirmation line:

**Azure DevOps wiki:**
```
Release notes published: Sprint 42 — 20 items — https://dev.azure.com/<org>/<project>/_wiki/...
```

**Generic / no wiki URL configured:**
```
Release notes complete: Sprint 42 — 20 items — written to release-notes-Sprint-42.md
```

## Prerequisites

- `AZURE_TOKEN` — PAT with `Work Items (Read)` and `Wiki (Read & Write)` scopes
- `AZURE_DEVOPS_WIKI_URL` — target wiki page URL (optional; falls back to local file if not set)

---

Starting release note generation now...
