# MCP Configuration — Runtime Setup

The `requirement-analyst` plugin connects to GitHub via an MCP server for reading issues and codebase files. You must supply your own GitHub token at runtime using one of the methods below.

---

## Option 1: Personal config file (recommended)

Create a local MCP config file that lives outside the repository and is never committed:

```bash
mkdir -p ~/.claude
```

Create `~/.claude/my-mcp-config.json` with your real token:

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

Then launch Claude Code pointing to that file:

```bash
claude --mcp-config ~/.claude/my-mcp-config.json
```

> The `--mcp-config` flag overrides the plugin's built-in `mcp-config.json` for the session. Your personal file is never touched by the repository.

---

## Option 2: Environment variable substitution

If you prefer to keep a single config file, update `~/.claude/my-mcp-config.json` to reference an environment variable instead of a hardcoded token:

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

Export the variable in your shell before launching:

```bash
export GITHUB_TOKEN=ghp_your_actual_token_here
claude --mcp-config ~/.claude/my-mcp-config.json
```

Add the export to your `~/.zshrc` or `~/.bashrc` to make it permanent:

```bash
echo 'export GITHUB_TOKEN=ghp_your_actual_token_here' >> ~/.zshrc
source ~/.zshrc
```

---

## Generating a GitHub Token

1. Go to [github.com/settings/tokens](https://github.com/settings/tokens)
2. Click **Generate new token (classic)**
3. Select the following scopes:
   - `repo` — full repository access (required to read/write issues and file contents)
   - `read:org` — read org membership (optional, for org-owned repos)
4. Copy the generated token and use it in your config file

---

## Verification

After launching with `--mcp-config`, confirm the GitHub MCP server is connected:

```
/mcp
```

You should see `github` listed as a connected server with status `connected`. If it shows an error, check that your token is valid and has the required scopes.

---

## Summary

| Method | Token location | Committed to repo? |
|---|---|---|
| `--mcp-config` with hardcoded token | `~/.claude/my-mcp-config.json` | No |
| `--mcp-config` with `${GITHUB_TOKEN}` | Shell environment / `.zshrc` | No |

---

## Note

Unlike the `pr-review` plugin, the `requirement-analyst` plugin does **not** require a `GIT_TOKEN` for git push operations. It only reads the codebase and writes to GitHub Issues via the MCP API. A single `GITHUB_TOKEN` with `repo` scope is sufficient.
