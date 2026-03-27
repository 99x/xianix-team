# Provider: Azure DevOps

Use this provider when `PLATFORM=azure-devops` or `git remote get-url origin` contains `dev.azure.com` or `visualstudio.com`.

## Prerequisites

All Azure DevOps API calls are made directly via `curl` using a Personal Access Token (PAT).

Required environment variables:

| Variable | Purpose |
|---|---|
| `AZURE_TOKEN` | Azure DevOps PAT — must have `Work Items (Read)` and `Wiki (Read & Write)` scopes |
| `AZURE_DEVOPS_WIKI_URL` | Full wiki page URL — e.g. `https://dev.azure.com/{org}/{project}/_wiki/wikis/{wiki-id}?pagePath=/Release-Notes` |

Optional — override values otherwise parsed from the remote URL or wiki URL:

| Variable | Default |
|---|---|
| `AZURE_ORG` | Parsed from remote URL |
| `AZURE_PROJECT` | Parsed from remote URL |
| `AZURE_DEVOPS_ITERATION_PATH_PREFIX` | Parsed from `AZURE_PROJECT` |
| `AZURE_DEVOPS_WORK_ITEM_TYPES` | `User Story,Bug,Feature,Task,Epic` |

---

## Parsing the Remote URL

Extract org and project from the remote URL before making any API calls.

**HTTPS format:** `https://dev.azure.com/{org}/{project}/_git/{repo}`

```bash
REMOTE=$(git remote get-url origin)
AZURE_ORG=$(echo "$REMOTE"     | sed 's|https://dev.azure.com/||' | cut -d'/' -f1)
AZURE_PROJECT=$(echo "$REMOTE" | sed 's|https://dev.azure.com/||' | cut -d'/' -f2)
```

**Legacy HTTPS format:** `https://{org}.visualstudio.com/{project}/_git/{repo}`

```bash
AZURE_ORG=$(echo "$REMOTE"     | sed 's|https://||' | cut -d'.' -f1)
AZURE_PROJECT=$(echo "$REMOTE" | cut -d'/' -f4)
```

---

## Parsing the Wiki URL

`AZURE_DEVOPS_WIKI_URL` accepts the URL exactly as copied from the browser — no reformatting needed. Both formats produced by Azure DevOps are supported:

**Browser navigation URL** (most common — just copy from the address bar):
```
https://dev.azure.com/org/project/_wiki/wikis/project.wiki/1/My-Page
```

**Query-string URL** (from sharing or API links):
```
https://dev.azure.com/org/project/_wiki/wikis/project.wiki?pagePath=/My-Page
```

Parse the wiki ID and page path:

```bash
WIKI_URL="${AZURE_DEVOPS_WIKI_URL}"

# Extract wiki identifier (the segment immediately after /wikis/)
WIKI_ID=$(echo "$WIKI_URL" | sed 's|.*/_wiki/wikis/||' | cut -d'/' -f1 | cut -d'?' -f1)

# Extract page path — handle both URL formats:
#   Browser format: .../wikis/{wikiId}/{pageId}/{page-name}  → /page-name
#   Query format:   .../wikis/{wikiId}?pagePath=/page-name   → /page-name
if echo "$WIKI_URL" | grep -q 'pagePath='; then
    # Query-string format
    PAGE_PATH_BASE=$(echo "$WIKI_URL" | grep -oP 'pagePath=\K[^&]+' | python3 -c "import sys,urllib.parse; print(urllib.parse.unquote(sys.stdin.read().strip()))")
else
    # Browser navigation format — path segments after {wikiId}/{pageId}/
    PAGE_PATH_BASE=$(echo "$WIKI_URL" | sed 's|.*/_wiki/wikis/[^/]*/[0-9]*/||' | sed 's|?.*||' | python3 -c "import sys,urllib.parse; print('/' + urllib.parse.unquote(sys.stdin.read().strip()))")
fi
```

---

## Publishing the Release Note

### Step 1: Derive the wiki page path for this release

Sanitize the sprint or tag name for use as a wiki page path (spaces → hyphens, remove special chars):

```bash
# e.g. "Sprint 42" → "Sprint-42", "v1.4.2" → "v1.4.2"
PAGE_NAME=$(echo "${SPRINT_OR_TAG}" | sed 's/ /-/g; s/[^a-zA-Z0-9._-]//g')
FULL_PAGE_PATH="${PAGE_PATH_BASE}/${PAGE_NAME}"
```

### Step 2: Check if the page already exists (get eTag for concurrency control)

```bash
HTTP_RESPONSE=$(curl -s -w "\n%{http_code}" \
  -u ":${AZURE_TOKEN}" \
  "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/wiki/wikis/${WIKI_ID}/pages?path=$(python3 -c "import urllib.parse,sys; print(urllib.parse.quote(sys.stdin.read().strip()))" <<< "${FULL_PAGE_PATH}")&api-version=7.1")

HTTP_BODY=$(echo "$HTTP_RESPONSE" | head -n -1)
HTTP_CODE=$(echo "$HTTP_RESPONSE" | tail -n 1)

if [ "$HTTP_CODE" = "200" ]; then
  ETAG=$(echo "$HTTP_BODY" | python3 -c "import json,sys; print(json.load(sys.stdin).get('eTag','*'))")
else
  ETAG="*"  # New page — no concurrency conflict possible
fi
```

### Step 3: Create or update the wiki page

Use the `If-Match` header with the eTag to prevent overwriting a page that was modified by another process since the check.

```bash
curl -s \
  -u ":${AZURE_TOKEN}" \
  -X PUT \
  -H "Content-Type: application/json" \
  -H "If-Match: ${ETAG}" \
  "https://dev.azure.com/${AZURE_ORG}/${AZURE_PROJECT}/_apis/wiki/wikis/${WIKI_ID}/pages?path=$(python3 -c "import urllib.parse,sys; print(urllib.parse.quote(sys.stdin.read().strip()))" <<< "${FULL_PAGE_PATH}")&api-version=7.1" \
  -d "$(python3 -c "
import json, sys
content = sys.stdin.read()
print(json.dumps({'content': content}))
" <<< "${RELEASE_NOTE_MARKDOWN}")"
```

If the response is HTTP 412 (Precondition Failed), the page was modified concurrently — re-fetch the eTag and retry once.

---

## Output

On completion:

```
Release notes published: <sprint-name> — <N> items — https://dev.azure.com/<org>/<project>/_wiki/wikis/<wiki-id>?pagePath=<page-path>
```
