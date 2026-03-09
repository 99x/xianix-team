# Git Authentication — Runtime Credentials

The `pr-review` plugin can apply code fixes and push them directly to the PR branch. Since the agent runtime may operate against **different repositories with different access levels**, git credentials are passed at runtime via environment variables — never hardcoded, never written to disk or `~/.gitconfig`.

---

## How it works

The plugin uses **`GIT_CONFIG_COUNT` environment variables** (Git 2.31+) to inject a token transparently into every `git push` command for the session. This rewrites any `https://github.com/` URL to use the token inline, scoped only to the current shell process:

```bash
GIT_CONFIG_COUNT=1
GIT_CONFIG_KEY_0="url.https://x-access-token:<token>@github.com/.insteadOf"
GIT_CONFIG_VALUE_0="https://github.com/"
```

This approach:
- Never touches `~/.gitconfig` or any file on disk
- Is scoped to the shell session — gone when the process exits
- Works across any GitHub HTTPS remote regardless of which repo is checked out
- Supports different tokens per invocation for different repo access levels

The `validate-prerequisites.sh` hook sets this up automatically before every `git push`, as long as `GIT_TOKEN` is present in the environment.

---

## Two tokens, two purposes

| Variable | Used by | Purpose |
|---|---|---|
| `GITHUB_TOKEN` | GitHub MCP server | Read PR metadata, post review comments via GitHub API |
| `GIT_TOKEN` | Local git push/pull | Authenticate HTTPS pushes to the PR branch |

These are typically the same personal access token, but they can differ — e.g. if your MCP server uses a GitHub App token and git uses a PAT.

---

## Passing credentials at runtime

### Inline (single session)

```bash
GITHUB_TOKEN=ghp_xxx GIT_TOKEN=ghp_xxx claude --mcp-config ~/.claude/my-mcp-config.json
```

Both tokens are set for the duration of the Claude Code session only.

### Via shell export (persistent in current shell)

```bash
export GITHUB_TOKEN=ghp_xxx
export GIT_TOKEN=ghp_xxx
claude --mcp-config ~/.claude/my-mcp-config.json
```

### Via `.env` file (per-project, never committed)

Create a `.env` file in your project root (add it to `.gitignore`):

```bash
GITHUB_TOKEN=ghp_xxx
GIT_TOKEN=ghp_xxx
```

Then source it before launching:

```bash
source .env && claude --mcp-config ~/.claude/my-mcp-config.json
```

---

## Using different tokens per repository

Because credentials are passed at invocation time, you can use a different token for each repo:

```bash
# Reviewing a public repo
GIT_TOKEN=ghp_public_repo_token claude --mcp-config ~/.claude/my-mcp-config.json

# Reviewing a private org repo
GIT_TOKEN=ghp_org_repo_token claude --mcp-config ~/.claude/my-mcp-config.json
```

No global config changes — each session is fully isolated.

---

## Generating a token with the right scopes

The token used for `GIT_TOKEN` needs write access to push:

1. Go to [github.com/settings/tokens](https://github.com/settings/tokens)
2. Click **Generate new token (classic)**
3. Select scopes:
   - `repo` — required for push access to private repos
   - `public_repo` — sufficient for push access to public repos only
4. For org repos, ensure the token is authorised for SSO if your org requires it

---

## What happens if GIT_TOKEN is missing

The `validate-prerequisites.sh` hook blocks any `git push` attempt if `GIT_TOKEN` is not set:

```
blocked: GIT_TOKEN is not set. Pass it at runtime: GIT_TOKEN=ghp_xxx claude ... (see docs/git-auth.md)
```

`git commit` and other local operations are unaffected — only push requires the token.

---

## Verification

After launching with the token set, verify git can push by running a dry-run:

```bash
git push --dry-run origin HEAD
```

If it completes without a credential prompt, the token is injected correctly.

---

## Summary

| What to set | When |
|---|---|
| `GIT_TOKEN` | Any session where the agent will push code fixes |
| `GITHUB_TOKEN` | Always — required for MCP GitHub API access |
| Both as same value | Simplest setup if one PAT covers both API and git push |
| Different values | When using GitHub App tokens for API vs PAT for git |
