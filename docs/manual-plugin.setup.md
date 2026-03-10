# Manual Plugin Setup — Using Plugins from a Git Repo as Marketplace

Developers can use plugins like **pr-review** by connecting Claude Code to the Xianix git repository as a plugin marketplace.

## Prerequisites

- [Claude Code](https://code.claude.com/) installed
- Git credentials configured (for private repos, ensure `gh auth login` or equivalent works)

## Steps

### 1. Add the repository as a marketplace

In Claude Code, run:

```
/plugin marketplace add 99x/xianix-team
```

For other git hosts, use the full URL:

```
/plugin marketplace add https://github.com/99x/xianix-team.git
```

To pin a specific branch or tag:

```
/plugin marketplace add https://github.com/99x/xianix-team.git#main
/plugin marketplace add https://github.com/99x/xianix-team.git#v1.0.0
```

> **Note:** The repository must contain `.claude-plugin/marketplace.json` at its root to be used as a marketplace.

### 2. Install the plugin

Once the marketplace is added:

```
/plugin install pr-review-plugin@xianix-tools
```

### 3. Refresh updates

To pull the latest plugin versions from the repo:

```
/plugin marketplace update
```

---

## Related setup

- **MCP & GitHub auth**: See [plugins/pr-review/docs/mcp-config.md](../plugins/pr-review/docs/mcp-config.md) and [plugins/pr-review/docs/git-auth.md](../plugins/pr-review/docs/git-auth.md).
