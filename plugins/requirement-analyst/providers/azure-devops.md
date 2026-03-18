# Provider: Azure DevOps

Use this provider when `git remote get-url origin` contains `dev.azure.com` or `visualstudio.com`.

## Prerequisites

The Azure DevOps REST API is called directly via `curl` using a Personal Access Token (PAT).

Required environment variable:

| Variable | Purpose |
|---|---|
| `AZURE_TOKEN` | Azure DevOps PAT — must have `Work Items (Read & Write)` scopes |

Optional — used to override values parsed from the remote URL:

| Variable | Default |
|---|---|
| `AZURE_ORG` | Parsed from remote URL |
| `AZURE_PROJECT` | Parsed from remote URL |

---

## Parsing the Remote URL

Extract org and project from the remote URL before making any API calls.

**HTTPS format:** `https://dev.azure.com/{org}/{project}/_git/{repo}`

```bash
REMOTE=$(git remote get-url origin)

AZURE_ORG=$(echo "$REMOTE"   | sed 's|https://dev.azure.com/||' | cut -d'/' -f1)
AZURE_PROJECT=$(echo "$REMOTE" | sed 's|https://dev.azure.com/||' | cut -d'/' -f2)
```

---

## Fetching Work Item Details

```bash
curl -s -u ":${AZURE_TOKEN}" \
  "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/wit/workitems/${WORK_ITEM_ID}?api-version=7.1&\$expand=all"
```

---

## Posting the Elaboration

### 1. Update the work item description

```bash
curl -s -u ":${AZURE_TOKEN}" \
  -X PATCH \
  -H "Content-Type: application/json-patch+json" \
  "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/wit/workitems/${WORK_ITEM_ID}?api-version=7.1" \
  -d "$(python3 -c "
import json
print(json.dumps([
  {'op': 'replace', 'path': '/fields/System.Description', 'value': '''${ELABORATION_BODY}'''},
  {'op': 'add', 'path': '/fields/System.Tags', 'value': '${VERDICT_TAG}'}
]))
")"
```

### 2. Add a comment with unresolved questions

```bash
curl -s -u ":${AZURE_TOKEN}" \
  -X POST \
  -H "Content-Type: application/json" \
  "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/wit/workitems/${WORK_ITEM_ID}/comments?api-version=7.1-preview.4" \
  -d "$(python3 -c "
import json
print(json.dumps({'text': '''${QUESTION_BODY}'''}))
")"
```

### 3. Map verdict to Azure DevOps tag

| Plugin verdict | Azure DevOps tag |
|---|---|
| `GROOMED` | `groomed` |
| `NEEDS CLARIFICATION` | `needs-clarification` |
| `NEEDS DECOMPOSITION` | `needs-decomposition` |

---

## Output

On completion:

```
Elaboration posted on work item #<id>: <verdict> — <N> acceptance criteria — <N> unresolved questions
```
