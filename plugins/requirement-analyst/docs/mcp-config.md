# MCP Configuration — Runtime Setup

The `requirement-analyst` plugin connects to **GitHub** via MCP for reading issues and codebase files. Optionally, it uses **Tavily** for web search to bring competitive and market context (similar implementations, competitor approaches, industry patterns).

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

## Web search (competitive & market context)

The **competitive-context-analyst** needs web search. Two options:

### DuckDuckGo (recommended — no API key)

Uses [@oevortex/ddg_search](https://github.com/OEvortex/ddg_search), which scrapes DuckDuckGo:

```json
"ddg_search": {
  "command": "npx",
  "args": ["-y", "@oevortex/ddg_search@latest"]
}
```

### Tavily (optional — higher quality, requires API key)

Get an API key at [tavily.com](https://www.tavily.com/):

```json
"tavily": {
  "command": "npx",
  "args": ["-y", "tavily-mcp@latest"],
  "env": { "TAVILY_API_KEY": "tvly_your_key" }
}
```

If neither is configured, the competitive-context-analyst outputs *"Web search not configured."* The rest of the elaboration still runs.

---

## Verification

After launching with `--mcp-config`, confirm servers are connected:

```
/mcp
```

You should see `github` (and optionally `tavily`) listed with status `connected`.

---

## Summary

| Server | Purpose | Required? |
| ------ | ------- | --------- |
| GitHub | Read/write issues, codebase files | Yes (for GitHub repos) |
| ddg_search | Web search for competitive context (no API key) | No (recommended) |
| Tavily | Higher-quality web search (API key) | No |

---

## Note

Unlike the `pr-review` plugin, the `requirement-analyst` plugin does **not** require a `GIT_TOKEN` for git push operations. It only reads the codebase and writes to GitHub Issues via the MCP API. A single `GITHUB_TOKEN` with `repo` scope is sufficient.
