# Platform Setup Guide

This guide covers the credentials required to run `release-note-generator` against Azure DevOps.

---

## Azure DevOps

### Required

| Variable | Purpose |
|---|---|
| `AZURE_TOKEN` | Personal Access Token (PAT) with **Work Items (Read)** and **Wiki (Read & Write)** scopes |
| `SPRINT_NAME` | Sprint name exactly as it appears in Azure DevOps Boards |
| `AZURE_DEVOPS_WIKI_URL` | Wiki page URL to publish to (optional — omit to write to a local file instead) |

### How to generate a PAT

1. Go to `https://dev.azure.com/{yourorg}/_usersSettings/tokens`
2. Click **New Token**
3. Set expiry (recommend 90 days)
4. Under **Scopes**, select:
   - **Work Items**: Read
   - **Wiki**: Read & Write
5. Copy the token — it is shown only once

### Finding your wiki URL

1. Open your Azure DevOps project
2. Navigate to **Wiki** in the left sidebar
3. Go to the page where you want release notes saved (create it first if it doesn't exist)
4. **Copy the URL directly from your browser address bar** and paste it as-is into `AZURE_DEVOPS_WIKI_URL`

Both URL formats that Azure DevOps produces work:
```
https://dev.azure.com/org/project/_wiki/wikis/project.wiki/1/My-Page      ← browser address bar
https://dev.azure.com/org/project/_wiki/wikis/project.wiki?pagePath=/My-Page  ← share link
```

The org and project are derived automatically from this URL — no separate `AZURE_ORG` or `AZURE_PROJECT` needed.

### Optional environment variables

| Variable | Default | Purpose |
|---|---|---|
| `AZURE_DEVOPS_ITERATION_PATH_PREFIX` | Same as project name | Prefix for sprint iteration paths |
| `AZURE_DEVOPS_WORK_ITEM_TYPES` | `User Story,Bug,Feature,Task,Epic` | Work item types to include |

---

## Minimal `.env`

```bash
AZURE_TOKEN=your_pat_here
SPRINT_NAME=Sprint 42
AZURE_DEVOPS_WIKI_URL=https://dev.azure.com/yourorg/yourproject/_wiki/wikis/yourproject.wiki/1/Release-Notes
```
