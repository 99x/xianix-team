# Provider: Azure DevOps

Use this provider when `git remote get-url origin` contains `dev.azure.com` or `visualstudio.com`.

## Prerequisites

The Azure DevOps REST API is called directly via `curl` using a Personal Access Token (PAT).

| Variable | Purpose |
|---|---|
| `AZURE_TOKEN` | Azure DevOps PAT — must have `Code (Read)` + `Pull Request Threads (Read & Write)` scopes |

---

## Parsing the Remote URL

Extract org, project, and repo from the remote URL before making any API calls.

**HTTPS format:** `https://dev.azure.com/{org}/{project}/_git/{repo}`

```bash
REMOTE=$(git remote get-url origin)
AZURE_ORG=$(echo "$REMOTE"     | sed 's|https://dev.azure.com/||' | cut -d'/' -f1)
AZURE_PROJECT=$(echo "$REMOTE" | sed 's|https://dev.azure.com/||' | cut -d'/' -f2)
AZURE_REPO=$(echo "$REMOTE"    | sed 's|https://dev.azure.com/||' | cut -d'/' -f4)
```

---

## Posting the Security Review

### Post a PR thread (review comment)

```bash
curl -s -u ":${AZURE_TOKEN}" \
  -X POST \
  -H "Content-Type: application/json" \
  "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/git/repositories/${AZURE_REPO}/pullRequests/${PR_NUMBER}/threads?api-version=7.1" \
  -d "$(python3 -c "
import json
print(json.dumps({
  'comments': [{'content': '''${SECURITY_REPORT}''', 'commentType': 1}],
  'status': 1
}))
")"
```

### Map verdict to thread status

| Verdict | Thread status |
|---|---|
| `APPROVED` | `fixed` (1) |
| `APPROVED WITH SUGGESTIONS` | `active` (1) |
| `CHANGES REQUESTED` | `active` (1) |

---

## Output

On completion:

```
Security review posted on PR #<number>: <verdict> — <N> findings (<N> critical, <N> high, <N> medium, <N> low)
```
