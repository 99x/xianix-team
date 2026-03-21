# Xianix Team — AI-Augmented Software Development

> Humans and AI agents, working as one team across the full software development lifecycle.

Xianix Team embeds a coordinated mesh of AI agents into every phase of the SDLC — from requirement analysis and sprint planning through to PR review, test strategy, and documentation maintenance. The goal is **amplification, not replacement**: human engineers operate at 10x efficacy while agents handle the repetitive, detail-heavy work that keeps quality and standards from slipping.

The first agent shipped is the **PR Review Agent** — a fully autonomous code reviewer that triggers on pull requests, analyses the diff against architecture rules and coding standards, and posts structured feedback directly on the PR.

---

## Documentation

### Concepts & Architecture

| Document | Description |
|----------|-------------|
| [docs/concept.md](docs/concept.md) | Vision, the full SDLC agent pipeline, and the agent mesh model |
| [docs/agent-architecture.md](docs/agent-architecture.md) | Technical architecture: Agent Control Plane, webhook flow, Claude Code plugin system |
| [docs/webhook-provider-design.md](docs/webhook-provider-design.md) | Webhook parsing design — provider identification, unified PR context model, GitHub vs Azure DevOps |

### Setup & Usage

| Document | Description |
|----------|-------------|
| [docs/manual-plugin.setup.md](docs/manual-plugin.setup.md) | Install and use the PR Review plugin manually via Claude Code |
| [plugins/pr-reviewer/docs/platform-setup.md](plugins/pr-reviewer/docs/platform-setup.md) | Platform setup for GitHub and Azure DevOps (review posting, tokens) |
| [plugins/pr-reviewer/docs/git-auth.md](plugins/pr-reviewer/docs/git-auth.md) | Runtime git credentials — how tokens are passed for clone and push |
| [plugins/pr-reviewer/docs/mcp-config.md](plugins/pr-reviewer/docs/mcp-config.md) | MCP configuration reference (superseded by platform-setup.md) |

### Deployment

| Document | Description |
|----------|-------------|
| [docs/docker-deployment.md](docs/docker-deployment.md) | Running the PR Review agent in Docker (build, run, volumes, CI/CD publish) |
| [docs/dockerhub-publishing.md](docs/dockerhub-publishing.md) | Publishing the Docker image to Docker Hub via GitHub Actions |

---

## PR Review Agent — Architecture

The PR Review Agent connects to the **Xians Agent Control Plane (ACP)**, which receives GitHub / Azure DevOps webhooks and relays them to the locally-running console process — no inbound port or public IP required.

### End-to-end flow

```
GitHub / ADO
  │  PR opened or synchronized → webhook POST
  ▼
Xians Agent Control Plane (ACP)
  │  cloud relay — routes event to tenant's registered agent
  ▼
AgentTeam.Console  (Program.cs — .NET 9)
  │  connects to ACP via XIANS_SERVER_URL + XIANS_API_KEY
  │  PrReviewAgent.Register() → DefineIntegrator().OnWebhook()
  ▼
PrReviewAgent  (webhook handler)
  │  WebhookParserResolver → GitHubWebhookParser | AzureDevOpsWebhookParser
  │  extracts: PlatformName, RepoUrl, PrNumber, SourceBranch
  │  builds deterministic workflowId (deduplicates re-delivered webhooks)
  ▼
PrReviewScriptWorkflow  (Temporal — durable, resumable)
  │  ActivityOptions: 20 min timeout, MaxAttempts = 1 (script is not idempotent)
  ▼
RunPrReviewScriptActivity
  │  resolves scripts/run-pr-review.sh from XIANIX_REPO_ROOT or binary path
  │  injects env vars: PLATFORM, REPO_URL, PR_NUMBER, PR_SOURCE_REF
  │  inherits process env so tokens (GITHUB_TOKEN / AZURE_TOKEN) flow through
  ▼
bash scripts/run-pr-review.sh
  │
  ├── git clone --bare          (first run → REPO_CACHE_DIR, shared across concurrent PRs)
  ├── git fetch --prune         (subsequent runs — incremental only)
  ├── git worktree add          (isolated per-run checkout → WORKDIR)
  ├── git fetch refs/pull/<PR>/head
  ├── git clone xianix-team     (first run → XIANIX_CACHE_DIR)
  ├── git pull xianix-team      (subsequent runs — update plugin to latest)
  ├── write MCP config          (token injected at runtime, never committed)
  └── claude: /review-pr <PR_NUMBER> [--fix]
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
  └── git worktree remove  (cleanup — bare cache kept for next run)
```

Each PR review runs in its own `git worktree` — an independent checkout backed by a single shared bare clone. Concurrent reviews share the object store without conflict.

---

## Required environment variables

### GitHub

| Variable | Description |
|---|---|
| `PLATFORM` | `github` |
| `REPO_URL` | HTTPS clone URL, e.g. `https://github.com/org/repo.git` |
| `PR_NUMBER` | PR number to review |
| `GITHUB_TOKEN` | PAT with `repo` and `pull_requests` scopes |

### Azure DevOps

| Variable | Description |
|---|---|
| `PLATFORM` | `azure-devops` |
| `REPO_URL` | HTTPS clone URL, e.g. `https://dev.azure.com/org/project/_git/repo` |
| `PR_NUMBER` | Pull Request ID to review |
| `AZURE_TOKEN` | PAT with `Code (Read)` + `Pull Request Threads (Read & Write)` scopes |
| `GIT_TOKEN` | PAT for git clone/push (often same as `AZURE_TOKEN`) |

### Xians ACP (Console App)

| Variable | Description |
|---|---|
| `XIANS_SERVER_URL` | ACP endpoint, e.g. `https://api.agentri.ai` |
| `XIANS_API_KEY` | API key for tenant authentication |
| `ANTHROPIC_API_KEY` | Anthropic key passed through to Claude Code |

### Optional

| Variable | Default | Description |
|---|---|---|
| `XIANIX_REPO` | `https://github.com/99x/xianix-team.git` | Override the plugin marketplace repo URL |
| `XIANIX_CACHE_DIR` | `/tmp/pr-review-cache/xianix-team` | Local path for the cloned xianix-team plugin repo |
| `REPO_CACHE_DIR` | `/tmp/pr-review-cache/<repo-slug>` | Directory for the shared bare clone |
| `WORKDIR` | `/tmp/pr-review-<PR>-<timestamp>` | Per-run worktree directory (isolated, removed after run) |
| `XIANIX_REPO_ROOT` | *(auto-detected)* | Override path to the repo root containing `scripts/run-pr-review.sh` |
| `PR_REVIEW_TIMEOUT_MINUTES` | `20` | Activity timeout for the Temporal workflow |
| `FIX_MODE` | `""` | Set to `1` or `true` as an alternative to `--fix` |
| `KEEP_WORKDIR` | `0` | Set to `1` to preserve the worktree after the run (useful for debugging) |

---

## Running locally (direct script invocation)

### GitHub

```bash
PLATFORM=github \
REPO_URL=https://github.com/org/repo.git \
PR_NUMBER=123 \
GITHUB_TOKEN=ghp_xxx \
bash scripts/run-pr-review.sh
```

### Azure DevOps

```bash
PLATFORM=azure-devops \
REPO_URL=https://dev.azure.com/org/project/_git/repo \
PR_NUMBER=456 \
AZURE_TOKEN=pat_xxx \
GIT_TOKEN=pat_xxx \
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

## Deployment

The recommended production deployment runs the console app in Docker. See [docs/docker-deployment.md](docs/docker-deployment.md) for the full guide including Docker Compose setup, volumes, and CI/CD publishing.

---

## Security notes

- Tokens are passed as environment variables, never written to disk or committed.
- The clone uses in-URL token injection scoped to the process — `~/.gitconfig` is never modified.
- The MCP config is written to `~/.claude/mcp-config-pr-review.json` and deleted after the run.
- Use `--dangerously-skip-permissions` only inside isolated containers or VMs — never on a shared machine.
- Rotate tokens regularly; use fine-grained PATs with the minimum required scopes.
