# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Is

**Xianix Team** is a mesh of AI agents embedded into the SDLC. It consists of:
- A **.NET 9 agent host** (`AgentTeam.Console`) that connects to the Xians ACP, listens for webhooks, and dispatches Temporal workflows
- **Bash bootstrap scripts** (`scripts/`) that set up an isolated git worktree and invoke `claude` with the appropriate plugin
- **Claude Code plugins** (`plugins/`) that contain the actual agent logic as markdown prompt files

Three agents are implemented: PR Review, Impact Analysis, and Requirement Analysis. All follow the same pattern: webhook → Temporal workflow → bash script activity → `claude -p /command` with a plugin.

## Build & Test

```bash
# Build
dotnet build AgentTeam.Console/AgentTeam.Console.csproj

# Run tests
dotnet test tests/AgentTeam.Console.Tests/AgentTeam.Console.Tests.csproj

# Run a single test class
dotnet test tests/AgentTeam.Console.Tests/ --filter "FullyQualifiedName~GitHubWebhookParserTests"
```

## Smoke Tests (require `.env` with tokens)

```bash
bash tests/run-pr-review-test-gh.sh          # GitHub PR review
bash tests/run-pr-review-test-ado.sh         # Azure DevOps PR review
bash tests/run-req-analyst-test-gh.sh        # Requirement analysis
bash tests/run-impact-analysis-test-gh.sh    # Impact analysis
```

Each test script loads `AgentTeam.Console/.env`, sets platform/repo/PR defaults, and delegates to the corresponding `scripts/` bootstrap script. Override defaults via env vars:

```bash
PR_NUMBER=42 REPO_URL=https://github.com/org/repo.git bash tests/run-pr-review-test-gh.sh
```

## Architecture

### C# Agent Host (`AgentTeam.Console/`)

`Program.cs` initializes the Xians platform, registers two agent instances, and runs them concurrently:

1. **`xianixAgent`** — handles `pr-reviewer` and `req-analyst` webhooks via a single integrator workflow with a webhook name router
2. **`ImpactAnalysisAgent`** — self-contained agent that registers its own integrator and handles PR webhooks

Each agent registration follows: `platform.Agents.Register()` → `DefineCustom<Workflow>().AddActivity<Activity>()` → `DefineIntegrator().OnWebhook(...)` → `RunAllAsync()`.

**Webhook flow** (`Agents/` → `Workflows/`):
- Agent parses the raw webhook payload using `WebhookParserResolver` (handles GitHub + Azure DevOps, normalizes form-encoded payloads)
- Extracts `PrWebhookContext` / `IssueWebhookContext` and creates a deterministic `workflowId` to prevent duplicate processing on re-delivered webhooks
- Calls `XiansContext.Workflows.StartAsync<TWorkflow>(args, workflowId)`
- Each workflow has `MaxAttempts = 1` (scripts are not idempotent) and a 20-minute timeout (configurable via `*_TIMEOUT_MINUTES`)
- The activity resolves `XIANIX_REPO_ROOT`, injects env vars (`PLATFORM`, `REPO_URL`, `PR_NUMBER`, etc.), and shells out to `bash scripts/run-*.sh`

### Bootstrap Scripts (`scripts/`)

All three scripts share the same structure:
1. Acquire a flock on a **shared bare clone** (`REPO_CACHE_DIR`) — avoids re-cloning on every run
2. Create an **isolated git worktree** per run (`WORKDIR`) — safe for concurrent analyses
3. Fetch the PR branch into the worktree
4. Clone/update the **xianix-team plugin repo** to `XIANIX_CACHE_DIR` (skip with `XIANIX_USE_LOCAL=1` for local dev)
5. Write an ephemeral MCP config (GitHub token only; never committed)
6. Run `claude --plugin-dir <plugin> -p "/command <PR_NUMBER>"`
7. Remove the worktree on exit (trap cleanup)

### Claude Plugins (`plugins/`)

Each plugin is a directory of markdown files consumed by `claude --plugin-dir`:

| Plugin | Command | Orchestrator agent | Sub-agents |
|---|---|---|---|
| `pr-reviewer` | `/pr-review [PR]` | `pr-reviewer.md` | code-reviewer, security-reviewer, test-reviewer, performance-reviewer |
| `imp-analyst` | `/impact-analysis [PR]` | `imp-analyst.md` | change-scope-analyzer, dependency-tracer, feature-mapper, risk-assessor |
| `req-analyst` | `/requirement-analysis [issue]` | `req-analyst.md` | intent-analyst, domain-analyst, gap-risk-analyst |

Orchestrators pre-fetch all git diff data once, then launch sub-agents in parallel via the `Agent` tool, passing the diff as context (sub-agents do not re-run `git diff`). Platform-specific report posting logic lives in `providers/github.md`, `providers/azure-devops.md`, `providers/generic.md` within each plugin.

Plugin lifecycle hooks are defined in `hooks/hooks.json`. The plugin marketplace catalog is at `.claude-plugin/marketplace.json`.

## Required Environment Variables

Set in `AgentTeam.Console/.env`:

| Variable | Purpose |
|---|---|
| `XIANS_SERVER_URL` | Xians ACP endpoint |
| `XIANS_API_KEY` | Tenant API key |
| `ANTHROPIC_API_KEY` | Passed to Claude CLI |
| `GITHUB_TOKEN` | PAT with `repo` + `pull_requests` scopes |
| `AZURE_TOKEN` | PAT with Code (Read) + PR Threads (Read & Write) |
| `GIT_TOKEN` | Git clone token for Azure DevOps (often same as `AZURE_TOKEN`) |
| `GIT_USER_NAME` / `GIT_USER_EMAIL` | Git identity for commits in `--fix` mode |

## Key Behaviours to Know

- **Workflow deduplication**: Workflow IDs are deterministic (`pr-review-<platform>-<sanitized-repo>-<pr-number>`). Re-delivered webhooks are silently deduplicated by Temporal.
- **Local plugin dev**: Set `XIANIX_CACHE_DIR` to the repo root and `XIANIX_USE_LOCAL=1` to use local plugin files instead of cloning from GitHub.
- **`--fix` mode** (PR review only): Passes `FIX_MODE=1` to the script, which applies code fixes, commits them, and pushes to the PR branch.
- **Timeout tuning**: `PR_REVIEW_TIMEOUT_MINUTES`, `IMPACT_ANALYSIS_TIMEOUT_MINUTES` override the 20-minute default activity timeout.
- **Windows/Git Bash**: `python3` may not exist; the test scripts shim it with `python` automatically.
