# Provider: Azure DevOps

Use this provider when `git remote get-url origin` contains `dev.azure.com` or `visualstudio.com`.

## Prerequisites

The Azure DevOps REST API is called directly via `curl` using a Personal Access Token (PAT).

Required environment variable:

| Variable | Purpose |
|---|---|
| `AZURE_TOKEN` | Azure DevOps PAT — must have `Code (Read)` and `Pull Request Threads (Read & Write)` scopes |

Optional — used to override values parsed from the remote URL:

| Variable | Default |
|---|---|
| `AZURE_ORG` | Parsed from remote URL |
| `AZURE_PROJECT` | Parsed from remote URL |
| `AZURE_REPO` | Parsed from remote URL |

---

## Parsing the Remote URL

Extract org, project, and repo from the remote URL before making any API calls.

**HTTPS format:** `https://dev.azure.com/{org}/{project}/_git/{repo}`

```bash
REMOTE=$(git remote get-url origin)

# Extract components
AZURE_ORG=$(echo "$REMOTE"   | sed 's|https://dev.azure.com/||' | cut -d'/' -f1)
AZURE_PROJECT=$(echo "$REMOTE" | sed 's|https://dev.azure.com/||' | cut -d'/' -f2)
AZURE_REPO=$(echo "$REMOTE"  | sed 's|.*/_git/||' | sed 's|\.git$||')
```

**Legacy HTTPS format:** `https://{org}.visualstudio.com/{project}/_git/{repo}`

```bash
AZURE_ORG=$(echo "$REMOTE"   | sed 's|https://||' | cut -d'.' -f1)
AZURE_PROJECT=$(echo "$REMOTE" | cut -d'/' -f4)
AZURE_REPO=$(echo "$REMOTE"  | sed 's|.*/_git/||' | sed 's|\.git$||')
```

---

## Resolving the PR Number

If no PR number was passed as an argument, find the active PR for the current branch:

```bash
BRANCH=$(git rev-parse --abbrev-ref HEAD)

curl -s -u ":${AZURE_TOKEN}" \
  "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/git/repositories/${AZURE_REPO}/pullrequests?searchCriteria.sourceRefName=refs/heads/${BRANCH}&searchCriteria.status=active&api-version=7.1" \
  | python3 -c "import sys,json; prs=json.load(sys.stdin)['value']; print(prs[0]['pullRequestId'] if prs else '')"
```

Store the result as `PR_ID`. If empty, the branch has no open PR — output a warning and skip posting.

---

## Posting the Impact Report

Post the compiled impact report as a new comment thread on the PR:

```bash
curl -s -u ":${AZURE_TOKEN}" \
  -X POST \
  -H "Content-Type: application/json" \
  "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/git/repositories/${AZURE_REPO}/pullrequests/${PR_ID}/threads?api-version=7.1" \
  -d "$(python3 -c "
import json, sys
body = sys.stdin.read()
print(json.dumps({
  'comments': [{'content': body, 'commentType': 1}],
  'status': 'active'
}))
" <<'REPORT'
${REPORT_BODY}
REPORT
)"
```

---

## Output

On completion:

```
Impact analysis posted on PR #<id>: <risk-level> — <N> high-risk areas — https://dev.azure.com/<org>/<project>/_git/<repo>/pullrequest/<id>
```
