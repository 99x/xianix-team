# Server Automation — Autonomous PR Review

## How it works

```
[Webhook / CI trigger]
        │
        ▼
scripts/run-pr-review.sh
        │
        ├── git clone --bare (first run only → REPO_CACHE_DIR)
        ├── git fetch --prune (subsequent runs — incremental update only)
        ├── git worktree add (isolated per-run checkout → WORKDIR)
        ├── git fetch refs/pull/<PR>/head  (checkout PR branch in worktree)
        ├── git clone xianix-team (first run only → XIANIX_CACHE_DIR)
        ├── git pull xianix-team (subsequent runs — update plugin to latest)
        ├── write MCP config (token injected, never committed)
        └── claude: /review-pr <PR_NUMBER> [--fix]  (--plugin-dir points to local clone)
                        │
                        ├── pr-reviewer agent
                        │       ├── code-reviewer
                        │       ├── security-reviewer
                        │       ├── test-reviewer
                        │       └── performance-reviewer
                        │
                        └── providers/github.md  |  providers/azure-devops.md
                                post review + inline comments
        │
        └── git worktree remove (cleanup — bare cache kept for next run)
```

### Concurrent safety

Each PR review runs in its own `git worktree` — an independent checkout backed by a single shared bare clone. Concurrent reviews of the same repository share the object store without conflict: no two runs share a working directory, so `git checkout`, file edits, and `--fix` commits never interfere with each other.

---

## Scripts

### `run-pr-review.sh`

The main entrypoint. Handles full bootstrap → plugin install → review for GitHub and Azure DevOps.

---

## GitHub

### Required environment variables

| Variable | Description |
|---|---|
| `PLATFORM` | `github` |
| `REPO_URL` | HTTPS clone URL, e.g. `https://github.com/org/repo.git` |
| `PR_NUMBER` | PR number to review |
| `GITHUB_TOKEN` | PAT with `repo` and `pull_requests` scopes |

### Minimal invocation

```bash
PLATFORM=github \
REPO_URL=https://github.com/org/repo.git \
PR_NUMBER=123 \
GITHUB_TOKEN=ghp_xxx \
bash scripts/run-pr-review.sh
```

### With fix mode (apply and push fixes)

```bash
PLATFORM=github \
REPO_URL=https://github.com/org/repo.git \
PR_NUMBER=123 \
GITHUB_TOKEN=ghp_xxx \
bash scripts/run-pr-review.sh --fix
```

---

## Azure DevOps

### Required environment variables

| Variable | Description |
|---|---|
| `PLATFORM` | `azure-devops` |
| `REPO_URL` | HTTPS clone URL, e.g. `https://dev.azure.com/org/project/_git/repo` |
| `PR_NUMBER` | Pull Request ID to review |
| `AZURE_TOKEN` | PAT with `Code (Read)` + `Pull Request Threads (Read & Write)` scopes |
| `GIT_TOKEN` | PAT for git clone/push (often same as `AZURE_TOKEN`) |

### Minimal invocation

```bash
PLATFORM=azure-devops \
REPO_URL=https://dev.azure.com/org/project/_git/repo \
PR_NUMBER=456 \
AZURE_TOKEN=pat_xxx \
GIT_TOKEN=pat_xxx \
bash scripts/run-pr-review.sh
```

---

## Optional variables

| Variable | Default | Description |
|---|---|---|
| `XIANIX_REPO` | `https://github.com/99x/xianix-team.git` | Override the plugin marketplace repo URL |
| `XIANIX_CACHE_DIR` | `/tmp/pr-review-cache/xianix-team` | Local path for the cloned xianix-team plugin repo. Persists between runs — mount a volume here in containers |
| `REPO_CACHE_DIR` | `/tmp/pr-review-cache/<repo-slug>` | Directory for the shared bare clone. Persists between runs — mount a volume here in containers |
| `WORKDIR` | `/tmp/pr-review-<PR>-<timestamp>` | Per-run worktree directory (isolated, removed after run) |
| `FIX_MODE` | `""` | Set to `1` or `true` as an alternative to `--fix` |
| `KEEP_WORKDIR` | `0` | Set to `1` to preserve the worktree after the run (useful for debugging) |

---

## Triggering from a webhook server

The script is designed to be invoked directly from a webhook handler. A minimal Node.js example:

```js
import { exec } from 'child_process'

app.post('/webhook/github', (req, res) => {
  const { action, pull_request, repository } = req.body
  if (action !== 'opened' && action !== 'synchronize') return res.sendStatus(200)

  const env = {
    ...process.env,
    PLATFORM:      'github',
    REPO_URL:      repository.clone_url,
    PR_NUMBER:     String(pull_request.number),
    GITHUB_TOKEN:  process.env.GITHUB_TOKEN,
  }

  exec('bash scripts/run-pr-review.sh', { env }, (err, stdout, stderr) => {
    if (err) console.error('Review failed:', stderr)
    else console.log(stdout)
  })

  res.sendStatus(202)
})
```

For Azure DevOps, replace the webhook payload parsing with the `resource.pullRequestId` and `resource.repository.remoteUrl` fields from the ADO service hook payload.

---

## Running in a container

For fully isolated server environments, run the script inside a container with the required tools pre-installed:

```dockerfile
FROM node:20-slim
RUN apt-get update && apt-get install -y git curl python3 && rm -rf /var/lib/apt/lists/*
RUN npm install -g @anthropic-ai/claude-code
COPY scripts/ /app/scripts/
WORKDIR /app
ENTRYPOINT ["bash", "scripts/run-pr-review.sh"]
```

```bash
docker run --rm \
  -e PLATFORM=github \
  -e REPO_URL=https://github.com/org/repo.git \
  -e PR_NUMBER=123 \
  -e GITHUB_TOKEN=ghp_xxx \
  -v /var/cache/pr-review:/tmp/pr-review-cache \
  xianix-pr-review
```

Mounting `/var/cache/pr-review` as the cache directory means both the bare clone and the xianix-team plugin repo persist across container restarts — only the first review for a given repo does a full clone.

---

## Security notes

- Tokens are passed as environment variables, never written to disk or committed.
- The clone uses an in-URL token injection scoped to the process — `~/.gitconfig` is never modified.
- The MCP config is written to `~/.claude/mcp-config-pr-review.json` and deleted after the run.
- Use `--dangerously-skip-permissions` only inside isolated containers or VMs — never on a shared machine.
- Rotate tokens regularly; use fine-grained PATs with the minimum required scopes.
