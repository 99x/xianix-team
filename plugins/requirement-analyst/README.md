# Requirement Analyst Plugin

> Autonomous requirement elaboration for backlog items. Analyzes codebase context, writes acceptance criteria, identifies dependencies, and detects gaps — then posts the elaborated result to GitHub or Azure DevOps.

## What This Plugin Does

The **requirement-analyst** plugin orchestrates a multi-dimensional analysis of backlog items (GitHub Issues or Azure DevOps Work Items) before sprint planning. It runs five specialized sub-agents in parallel and compiles their findings into a single structured requirement.

| Agent | Focus |
| ----- | ----- |
| **context-analyst** | Codebase and architecture — affected modules, related issues, existing patterns |
| **acceptance-criteria-writer** | Structured Given/When/Then criteria, edge cases, boundary conditions |
| **dependency-analyzer** | Dependencies, risks, constraints, assumptions |
| **gap-detector** | Ambiguities, missing information, contradictions, under-specification |
| **competitive-context-analyst** | Similar implementations, competitor approaches, industry patterns (via web search) |

### Output

The elaboration produces a structured requirement with:

- **Verdict:** `GROOMED` | `NEEDS CLARIFICATION` | `NEEDS DECOMPOSITION`
- Summary, Acceptance Criteria, Edge Cases, Dependencies, Risks & Constraints, Assumptions, Unresolved Questions, Architecture Notes, Competitive & Market Context
- Automatic posting to the backlog platform (issue body updated or posted as comment)

---

## Local Testing with Claude Code

Run the plugin interactively in your project using Claude Code (Claude CLI).

### Prerequisites

- [Claude Code](https://docs.anthropic.com/claude-code) installed (`claude` CLI)
- Git repository with a GitHub remote (for codebase analysis)
- GitHub Personal Access Token with `repo` scope
- Working directory: your project repo or a clone of the target repo

### 1. Point Claude Code at the plugin

Launch with the plugin directory and MCP config:

```bash
claude \
  --plugin-dir /path/to/xianix-team/plugins/requirement-analyst \
  --mcp-config ~/.claude/my-mcp-config.json
```

> Replace `/path/to/xianix-team` with the actual path — e.g. if you cloned xianix-team to `~/xianix-team`, use `~/xianix-team/plugins/requirement-analyst`.

### 2. Configure MCP (GitHub + web search)

Create `~/.claude/my-mcp-config.json` with your GitHub token. For **competitive/market context**, add a web search MCP. See [docs/mcp-config.md](docs/mcp-config.md) for details.

**Recommended (GitHub + DuckDuckGo — no API key):**

```json
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "ghp_your_token_here"
      }
    },
    "ddg_search": {
      "command": "npx",
      "args": ["-y", "@oevortex/ddg_search@latest"]
    }
  }
}
```

**Optional: Tavily** (higher quality search, requires API key from [tavily.com](https://www.tavily.com/)):

```json
"tavily": {
  "command": "npx",
  "args": ["-y", "tavily-mcp@latest"],
  "env": { "TAVILY_API_KEY": "tvly_your_key" }
}
```

```bash
export GITHUB_TOKEN=ghp_your_token_here
claude --plugin-dir /path/to/xianix-team/plugins/requirement-analyst --mcp-config ~/.claude/my-mcp-config.json
```

### 3. Run from your project repo

`cd` into your **project repository** (the one whose backlog you want to elaborate), then start Claude:

```bash
cd /path/to/your-project
claude --plugin-dir /path/to/xianix-team/plugins/requirement-analyst --mcp-config ~/.claude/my-mcp-config.json
```

### 4. Invoke the command

In the Claude chat:

```text
/requirement-analysis 42
```

Elaborate issue #42. The agent will fetch the issue, analyze the codebase, run all five analysts (including competitive/market research when Tavily is configured), and post the elaborated requirement to GitHub.

**Post as comment instead of updating the issue body:**

```text
/requirement-analysis 42 --comment
```

### Optional: Test without posting

To inspect the output without updating GitHub, you can ask Claude to run the analysis and show you the elaboration before posting — the command is designed to post automatically, but you can experiment with custom prompts in a separate chat to see the structure.

---

## Central Run with `run-requirement-analysis.sh`

The script `scripts/run-requirement-analysis.sh` is designed for **server/CI** runs: it clones the target repo into an isolated worktree, injects MCP config, runs the analysis, and cleans up. Use it when requirements analysis is triggered centrally (e.g. by a webhook or scheduler).

### Prerequisites (central run)

- `git` and `claude` CLI installed
- Environment variables set for the target platform (see below)

### Required Environment Variables

#### GitHub

| Variable | Description |
| -------- | ----------- |
| `PLATFORM` | `github` |
| `REPO_URL` | Full HTTPS clone URL, e.g. `https://github.com/org/repo.git` |
| `ISSUE_NUMBER` | GitHub issue number to elaborate |
| `GITHUB_TOKEN` | PAT with `repo` scope (used for MCP and git clone) |

#### Azure DevOps

| Variable | Description |
| -------- | ----------- |
| `PLATFORM` | `azure-devops` |
| `REPO_URL` | Full HTTPS clone URL, e.g. `https://dev.azure.com/org/project/_git/repo` |
| `ISSUE_NUMBER` | Work Item ID to elaborate |
| `AZURE_TOKEN` | PAT with Work Items (Read & Write) scopes |
| `GIT_TOKEN` | PAT for git clone (often same as `AZURE_TOKEN`) |

### Usage

Run from the **xianix-team** repo root:

```bash
# GitHub
PLATFORM=github \
REPO_URL=https://github.com/org/repo.git \
ISSUE_NUMBER=42 \
GITHUB_TOKEN=ghp_xxx \
./scripts/run-requirement-analysis.sh
```

```bash
# Post elaboration as comment instead of updating the issue body
PLATFORM=github \
REPO_URL=https://github.com/org/repo.git \
ISSUE_NUMBER=42 \
GITHUB_TOKEN=ghp_xxx \
./scripts/run-requirement-analysis.sh --comment
```

```bash
# Azure DevOps
PLATFORM=azure-devops \
REPO_URL=https://dev.azure.com/org/project/_git/repo \
ISSUE_NUMBER=123 \
AZURE_TOKEN=pat_xxx \
GIT_TOKEN=pat_xxx \
./scripts/run-requirement-analysis.sh
```

### What the Script Does

1. Creates or updates a shared **bare clone** of the target repo (`REPO_CACHE_DIR`)
2. Creates an isolated **per-run worktree** (`WORKDIR`)
3. Writes MCP config with injected token (never committed)
4. Clones/updates **xianix-team** and loads the requirement-analyst plugin
5. Runs `claude -p "/analyze-requirement <ISSUE_NUMBER>"` inside the worktree
6. Removes the worktree after the run (unless `KEEP_WORKDIR=1`)

### Optional Variables

| Variable | Default | Description |
| -------- | ------- | ----------- |
| `XIANIX_REPO` | `https://github.com/99x/xianix-team.git` | Override plugin source repo |
| `XIANIX_CACHE_DIR` | `/tmp/requirement-analysis-cache/xianix-team` | Path for cloned xianix-team |
| `XIANIX_USE_LOCAL` | `0` | Set to `1` to use XIANIX_CACHE_DIR as-is (no clone/pull) — for local dev |
| `TAVILY_API_KEY` | *(none)* | Tavily API key for higher-quality search. Optional — DuckDuckGo (no key) is used by default. |
| `REPO_CACHE_DIR` | `/tmp/requirement-analysis-cache/<repo-slug>` | Path for bare clone |
| `WORKDIR` | `/tmp/requirement-analysis-<ISSUE>-<timestamp>` | Per-run worktree |
| `KEEP_WORKDIR` | `0` | Set to `1` to preserve worktree after run (debugging) |

---

## Documentation

| Document | Description |
| -------- | ----------- |
| [docs/mcp-config.md](docs/mcp-config.md) | MCP configuration for GitHub |
| [docs/backlog-setup.md](docs/backlog-setup.md) | Backlog labels and setup for grooming |
| [providers/github.md](providers/github.md) | GitHub posting behavior |
| [providers/azure-devops.md](providers/azure-devops.md) | Azure DevOps posting behavior |
