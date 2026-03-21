# MCP Configuration — Runtime Setup

The `req-analyst` plugin connects to **GitHub** via MCP for reading and writing issues, and uses **DuckDuckGo** for web search (competitive insights, industry patterns).

---

## GitHub (required for GitHub repos)

Create a local MCP config file that lives outside the repository and is never committed:

```bash
mkdir -p ~/.claude
```

Create `~/.claude/my-mcp-config.json`:

```json
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "ghp_your_actual_token_here"
      }
    },
    "ddg_search": {
      "command": "npx",
      "args": ["-y", "@oevortex/ddg_search@latest"]
    }
  }
}
```

Then launch Claude Code pointing to that file:

```bash
claude --mcp-config ~/.claude/my-mcp-config.json
```

### Environment variable substitution

Use `${GITHUB_TOKEN}` in the config and export it before launching:

```bash
export GITHUB_TOKEN=ghp_your_actual_token_here
claude --mcp-config ~/.claude/my-mcp-config.json
```

### Generating a GitHub Token

1. Go to [github.com/settings/tokens](https://github.com/settings/tokens)
2. Click **Generate new token (classic)**
3. Select `repo` scope (required for issues and file contents)
4. Copy the token into your config

---

## Web Search (DuckDuckGo)

The **domain-analyst** uses DuckDuckGo web search for competitive insights, industry patterns, and market context.

Uses [@oevortex/ddg_search](https://github.com/OEvortex/ddg_search) — no API key required, just Node.js (`npx`).

```json
"ddg_search": {
  "command": "npx",
  "args": ["-y", "@oevortex/ddg_search@latest"]
}
```

### Prerequisites

- **Node.js** (v18+) — required for `npx` to run the MCP server
- No API key or account needed

If the DuckDuckGo MCP server is not configured, the domain-analyst outputs what it can from domain reasoning and notes the limitation. The rest of the elaboration still runs.

---

## Verification

After launching with `--mcp-config`, confirm servers are connected:

```
/mcp
```

You should see `github` and `ddg_search` listed with status `connected`.

---

## Summary

| Server | Purpose | Required? | Prerequisites |
| ------ | ------- | --------- | ------------- |
| GitHub | Read/write issues | Yes (for GitHub repos) | `GITHUB_TOKEN` with `repo` scope |
| ddg_search | Web search for domain/competitive context | Recommended | Node.js (no API key) |

---

## Note

Unlike the `pr-review` plugin, the `req-analyst` plugin only reads/writes GitHub Issues via the MCP API. A single `GITHUB_TOKEN` with `repo` scope is sufficient — no git clone or push operations required.
