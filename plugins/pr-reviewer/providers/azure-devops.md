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

## Posting the Starting Comment

Before running any analysis, post a plain PR comment thread to inform the author that a review is underway. This fires as the very first action on Azure DevOps, before sub-agents are launched.

Parse the remote URL first (see **Parsing the Remote URL** above), then call:

```bash
curl -s -u ":${AZURE_TOKEN}" \
  -X POST \
  -H "Content-Type: application/json" \
  "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/git/repositories/${AZURE_REPO}/pullrequests/${PR_ID}/threads?api-version=7.1" \
  -d '{"comments":[{"content":"🔍 **PR review in progress**\n\nI'\''m running a comprehensive review covering code quality, security, test coverage, and performance. The full results will be posted as a review comment when complete — this may take a few minutes.","commentType":1}],"status":"active"}'
```

If posting the starting comment fails, output a single warning line and continue — do not stop the review.

---

## Posting the Review

### 1. Map verdict to Azure DevOps vote

| Plugin verdict | Azure DevOps vote value | Description |
|---|---|---|
| `APPROVE` | `10` | Approved |
| `REQUEST CHANGES` | `-10` | Rejected |
| `NEEDS DISCUSSION` | `-5` | Waiting for author |

### 2. Post the overall verdict (reviewer vote)

```bash
curl -s -u ":${AZURE_TOKEN}" \
  -X PUT \
  -H "Content-Type: application/json" \
  "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/git/repositories/${AZURE_REPO}/pullrequests/${PR_ID}/reviewers/me?api-version=7.1" \
  -d "{\"vote\": ${VOTE}}"
```

### 3. Post the full report as a PR thread

Post the compiled review report body as a new comment thread:

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

### 4. Post inline comments (one thread per finding)

For each finding with a precise file path and line number:

```bash
curl -s -u ":${AZURE_TOKEN}" \
  -X POST \
  -H "Content-Type: application/json" \
  "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/git/repositories/${AZURE_REPO}/pullrequests/${PR_ID}/threads?api-version=7.1" \
  -d "$(python3 -c "
import json
print(json.dumps({
  'comments': [{'content': '${FINDING_BODY}', 'commentType': 1}],
  'status': 'active',
  'threadContext': {
    'filePath': '/${FILE_PATH}',
    'rightFileStart': {'line': ${LINE_NUMBER}, 'offset': 1},
    'rightFileEnd':   {'line': ${LINE_NUMBER}, 'offset': 1}
  }
}))
")"
```

Post all inline comments without pausing between them.

---

## Output

On completion:

```
Review posted on PR #<id>: <verdict> — <N> inline comments — https://dev.azure.com/<org>/<project>/_git/<repo>/pullrequest/<id>
```
