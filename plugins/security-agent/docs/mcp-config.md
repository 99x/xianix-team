# MCP Configuration

The security-agent uses the **GitHub MCP server** to read PR diffs and post review results.

## GitHub MCP Server

Add the following to your Claude MCP configuration (`~/.claude/mcp.json` or via Claude settings):

```json
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "<your-github-pat>"
      }
    }
  }
}
```

### Required GitHub PAT Scopes

| Scope | Purpose |
|---|---|
| `repo` | Read PR diffs and file contents |
| `pull_requests` | Post review comments and formal reviews |

### Verify Connection

Run `/mcp` in Claude Code — `github` should appear as `connected`.

---

## Azure DevOps

Azure DevOps does not use an MCP server. The agent calls the REST API directly via `curl` using the `AZURE_TOKEN` environment variable.

Set `AZURE_TOKEN` to a PAT with:
- `Code (Read)` scope
- `Pull Request Threads (Read & Write)` scope

---

## Server-Side (Webhook Automation)

When running via `scripts/run-security-review.sh`, the MCP config is written automatically by the script using the `GITHUB_TOKEN` environment variable. No manual configuration is needed.
