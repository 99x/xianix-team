# Platform Setup Guide

The `pr-review` plugin works with any git hosting platform. All diff analysis uses standard git commands. Only the **review posting** step is platform-specific.

---

## GitHub

### Option A: GitHub MCP Server (recommended for inline comments)

The MCP server enables the richest review experience — inline comments posted directly on PR files.

**Create a personal config file** (never committed):

```bash
mkdir -p ~/.claude
```

Create `~/.claude/my-mcp-config.json`:

```json
{
  "mcpServers": {
    "github": {
      "url": "https://api.github.com",
      "token": "ghp_your_actual_token_here"
    }
  }
}
```

Or using an environment variable:

```json
{
  "mcpServers": {
    "github": {
      "url": "https://api.github.com",
      "token": "${GITHUB_TOKEN}"
    }
  }
}
```

Launch with:

```bash
export GITHUB_TOKEN=ghp_your_token_here
claude --mcp-config ~/.claude/my-mcp-config.json
```

**Verify:** Run `/mcp` inside Claude Code — `github` should show as `connected`.

**Token scopes needed:** `repo` (private repos) or `public_repo` (public repos only), `read:org` (optional).

### Option B: `gh` CLI (fallback)

If MCP is unavailable, the plugin falls back to the `gh` CLI automatically.

```bash
# Install: https://cli.github.com
gh auth login
```

**Token scopes needed:** same as Option A.

### Credentials for `git push` (fix mode)

When using `--fix`, the agent pushes commits. Pass the token at runtime:

```bash
GIT_TOKEN=ghp_your_token_here claude ...
```

Or export in your shell:

```bash
export GIT_TOKEN=ghp_your_token_here
```

---

## Azure DevOps

### Prerequisites

Install the Azure CLI and the Azure DevOps extension:

```bash
# Install Azure CLI: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
az extension add --name azure-devops
```

### Authentication

**Option A: Interactive login**

```bash
az login
az devops configure --defaults organization=https://dev.azure.com/<your-org>
```

**Option B: Personal Access Token (recommended for CI or scripted use)**

```bash
export AZURE_DEVOPS_PAT=<your-pat>
echo $AZURE_DEVOPS_PAT | az devops login --org https://dev.azure.com/<your-org>
```

Add to `~/.zshrc` or `~/.bashrc` to persist:

```bash
export AZURE_DEVOPS_PAT=<your-pat>
```

**PAT scopes needed:**
- `Code` → Read & Write
- `Pull Request Threads` → Read & Write

### Credentials for `git push` (fix mode)

The plugin reuses `AZURE_DEVOPS_PAT` for `git push` credential injection automatically — no separate `GIT_TOKEN` is needed for Azure DevOps remotes.

### Generating a PAT

1. Go to `https://dev.azure.com/<your-org>/_usersSettings/tokens`
2. Click **New Token**
3. Set the scopes listed above
4. Copy the token and export it as `AZURE_DEVOPS_PAT`

---

## Bitbucket / Other Platforms

For platforms without native CLI support, the plugin writes the review report to `pr-review-report.md` in the repository root. You can then post it manually.

No additional setup is required beyond having a working git installation.

---

## Summary

| Platform | Review posting method | Token variable | Fix mode push |
|---|---|---|---|
| GitHub (MCP) | `mcp__github__create_pull_request_review` | `GITHUB_TOKEN` | `GIT_TOKEN` |
| GitHub (CLI) | `gh pr review` | `gh auth login` | `GIT_TOKEN` |
| Azure DevOps | `az repos pr` + REST API | `AZURE_DEVOPS_PAT` | `AZURE_DEVOPS_PAT` |
| Generic | Write to `pr-review-report.md` | — | `GIT_TOKEN` |

---

## Related

- `docs/git-auth.md` — details on how git credentials are injected at runtime without touching `~/.gitconfig`
- `providers/github.md` — GitHub-specific posting logic
- `providers/azure-devops.md` — Azure DevOps-specific posting logic
- `providers/generic.md` — fallback for unsupported platforms
