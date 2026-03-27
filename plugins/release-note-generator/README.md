# release-note-generator

Automated release note generation plugin for Claude Code. Fetches Azure DevOps work items by sprint/iteration via WIQL and publishes structured Markdown release notes to an Azure DevOps wiki page or a local file.

Ported and extended from [azure-devops-release-notes-mcp](https://github.com/Chamalasela/azure-devops-release-notes-mcp).

---

## Usage

```bash
/generate-release-note "Sprint 42"               # Fetch work items and publish to wiki
/generate-release-note "Sprint 42" --preview     # Preview only — do not publish
```

---

## How It Works

1. **change-analyst** fetches all work items for the sprint via WIQL batch API and classifies them by type
2. **content-writer** formats the items into structured Markdown following the style guide
3. **release-note-generator** (orchestrator) publishes the result to the Azure DevOps wiki

---

## Quick Start

```bash
export PLATFORM=azure-devops
export REPO_URL=https://dev.azure.com/yourorg/yourproject/_git/yourrepo
export SPRINT_NAME="Sprint 42"
export AZURE_TOKEN=your_pat
export AZURE_DEVOPS_WIKI_URL=https://dev.azure.com/yourorg/yourproject/_wiki/wikis/yourproject.wiki?pagePath=/Release-Notes

./tests/run-release-note-test-ado.sh
```

---

## Environment Variables

| Variable | Required | Purpose |
|---|---|---|
| `PLATFORM` | Yes | Must be `azure-devops` |
| `REPO_URL` | Yes | Target repository HTTPS URL |
| `SPRINT_NAME` | Yes | Sprint or iteration name (e.g. `Sprint 42`) |
| `AZURE_TOKEN` | Yes | PAT with Work Items Read + Wiki Read & Write |
| `AZURE_DEVOPS_WIKI_URL` | Optional | Target wiki page URL — omit to write to local file |
| `AZURE_DEVOPS_WORK_ITEM_TYPES` | Optional | Comma-separated types (default: `User Story,Bug,Feature,Task,Epic`) |
| `AZURE_DEVOPS_ITERATION_PATH_PREFIX` | Optional | Iteration path prefix (default: project name) |
| `AZURE_ORG` | Optional | Override org parsed from `REPO_URL` |
| `AZURE_PROJECT` | Optional | Override project parsed from `REPO_URL` |
| `RELEASE_PREVIEW_ONLY` | Optional | Set `1` to generate without publishing |

---

## Output Format

```markdown
## Release Notes — Sprint 42

*Generated: 2026-03-26T10:00:00Z*

---

| Type | Count |
|---|---|
| 🚀 Features | 3 |
| 📖 User Stories | 8 |
| 🐛 Bug Fixes | 5 |
| ✅ Tasks | 4 |
| 🏔️ Epics | 0 |

**Total: 20 items**

### 🚀 Features
| ID | Title | State | Assigned To |
|---|---|---|---|
| [#1234](url) | Add export to PDF | Done | Alice Smith |
...
```

---

## Platform Setup

See [docs/platform-setup.md](docs/platform-setup.md) for PAT generation, required scopes, and wiki URL format.

## Template Guide

See [docs/template-guide.md](docs/template-guide.md) for work item type filtering, section order, and wiki page naming.
