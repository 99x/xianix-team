# Docker Deployment Guide

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) 24+ with BuildKit enabled
- [Docker Compose](https://docs.docker.com/compose/install/) v2+
- Your `.env` file at `AgentTeam.Console/.env` (see below)

---

## Environment Variables

All secrets are passed at runtime — never baked into the image.

Create or verify `AgentTeam.Console/.env`:

```env
# Xians platform
XIANS_SERVER_URL=https://api.agentri.ai
XIANS_API_KEY=<your-xians-api-key>

# Anthropic (Claude)
ANTHROPIC_API_KEY=<your-anthropic-api-key>

# Git identity used for commits in --fix mode
GIT_USER_NAME=xianix-agent
GIT_USER_EMAIL=your-bot@example.com

# GitHub (leave empty if not using GitHub)
GITHUB_TOKEN=<github-pat>

# Azure DevOps (leave empty if not using ADO)
AZURE_TOKEN=<ado-pat>
GIT_TOKEN=<ado-pat>          # usually the same as AZURE_TOKEN
```

---

## Building the Image

### Option A — Docker Compose (recommended)

```bash
docker compose build
```

To force a full rebuild with no cache:

```bash
docker compose build --no-cache
```

### Option B — Plain Docker

```bash
docker build -t xianix-pr-review-agent:latest .
```

> **Note:** The first build takes several minutes — it installs Node.js, the Claude CLI, the GitHub CLI (`gh`), and the Azure CLI (`az`) with the `azure-devops` extension.

---

## Running the Container

### Option A — Docker Compose

```bash
docker compose --env-file AgentTeam.Console/.env up
```

Run in the background:

```bash
docker compose --env-file AgentTeam.Console/.env up -d
```

View logs:

```bash
docker compose logs -f pr-review-agent
```

Stop:

```bash
docker compose down
```

### Option B — Plain Docker

```bash
docker run --rm \
  --env-file AgentTeam.Console/.env \
  -v pr-review-cache:/tmp/pr-review-cache \
  -v claude-home:/root/.claude \
  xianix-pr-review-agent:latest
```

---

## Triggering from a Webhook Server

When running without the Console App (direct script invocation), the script can be called from any webhook handler. A minimal Node.js example for GitHub:

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

For Azure DevOps, replace the payload parsing with `resource.pullRequestId` and `resource.repository.remoteUrl` from the ADO service hook payload.

> In production, the recommended approach is the Console App + Xians ACP, which handles webhook routing, deduplication, and durable execution automatically. See the [README](../README.md) for the full architecture.

---

## How It Works Inside the Container

```
Xians webhook
      │
      ▼
AgentTeam.Console (.NET 9)
      │  parses webhook payload (GitHub / Azure DevOps)
      │  starts PrReviewScriptWorkflow (Temporal)
      ▼
RunPrReviewScriptActivity
      │  spawns bash scripts/run-pr-review.sh
      ▼
run-pr-review.sh
      │  bare-clones the target repo
      │  creates an isolated git worktree for the PR branch
      │  clones/updates the xianix-team plugin repo
      │  runs: claude --plugin-dir <plugins/pr-reviewer> -p "/review-pr <N>"
      ▼
Claude Code CLI
      │  reads the PR diff
      │  posts review comments via gh (GitHub) or az (Azure DevOps)
      └──────────────────────────────────────────────────────────────
```

The script resolves the plugin repo from `XIANIX_CACHE_DIR` (default: `/tmp/pr-review-cache/xianix-team`). The Docker Compose setup mounts this path as a named volume so the clone persists across container restarts.

---

## Persistent Volumes

| Volume | Container path | Purpose |
|---|---|---|
| `pr-review-cache` | `/tmp/pr-review-cache` | Bare-clone cache + xianix-team plugin repo |
| `claude-home` | `/root/.claude` | Claude CLI session cache and MCP config |

Volumes survive `docker compose down` and are reused on the next `up`, avoiding repeated git clones.

To wipe the cache and start fresh:

```bash
docker compose down -v
```

---

## CI/CD — Automated Docker Hub Publish

See [docs/dockerhub-publishing.md](dockerhub-publishing.md) for the full guide on setting up automated Docker Hub publishing via GitHub Actions.

---

## Updating the Image

After pulling changes to `scripts/` or the application code:

```bash
git pull
docker compose build
docker compose --env-file AgentTeam.Console/.env up -d
```

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `XIANS_SERVER_URL not found` | Ensure your `.env` file is passed via `--env-file` |
| `'claude' is not installed` | The image build step `npm install -g @anthropic-ai/claude-code` failed — rebuild with `--no-cache` |
| `run-pr-review.sh not found` | `XIANIX_REPO_ROOT` is not set or the `scripts/` layer was not copied — rebuild the image |
| `git clone` failures | The container needs outbound internet access on port 443 |
| Azure DevOps reviews not posted | Confirm `AZURE_TOKEN` has *Pull Request Threads (Read & Write)* scope |
| GitHub reviews not posted | Confirm `GITHUB_TOKEN` has `repo` and `pull_requests` scopes |
