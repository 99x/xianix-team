# Template Guide

This guide explains how the `content-writer` agent formats release notes and how to customise the output.

---

## Default Template Structure

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

---

### 🚀 Features

| ID | Title | State | Assigned To |
|---|---|---|---|
| [#1234](https://dev.azure.com/.../1234) | Add export to PDF | Done | Alice Smith |

...

---

*This release note was generated automatically.*
```

---

## Work Item Type Filtering

Control which work item types appear in the release notes:

```bash
export AZURE_DEVOPS_WORK_ITEM_TYPES="User Story,Bug,Feature"
# Excludes Task and Epic from the output
```

---

## Section Order

Fixed order defined in `styles/release-note.md`:

1. Features
2. User Stories
3. Bug Fixes
4. Tasks
5. Epics

Sections with zero items are omitted entirely.

---

## ID Link Format

All IDs are rendered as Markdown links pointing to the Azure DevOps work item:

```
[#ID](https://dev.azure.com/{org}/{project}/_workitems/edit/{ID})
```

---

## Wiki Page Naming

The wiki page path is derived from the sprint name argument:

| Sprint argument | Wiki page path |
|---|---|
| `Sprint 42` | `<base-path>/Sprint-42` |
| `Sprint 42 - Hotfix` | `<base-path>/Sprint-42---Hotfix` |

Sanitization: spaces → hyphens, characters outside `[a-zA-Z0-9._-]` are removed.

The base path comes from `AZURE_DEVOPS_WIKI_URL` (the `pagePath` query parameter).
