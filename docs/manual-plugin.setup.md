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
/plugin install pr-reviewer@xianix-tools
```

### 3. Refresh updates

To pull the latest plugin versions from the repo:

```
/plugin marketplace update
```

---

## Reviewing PRs

Once installed, run a PR review with the `/pr-review` command:

| Prompt | Description |
|--------|-------------|
| `/pr-review` | Review the current branch vs `main` |
| `/pr-review 123` | Review GitHub PR #123 |
| `/pr-review feature/foo` | Review branch `feature/foo` vs `main` |
| `/pr-review 123 --fix` | Apply and push fixes, then post the review |

The review is posted to GitHub automatically with inline comments.

### Invoking the pr-reviewer agent

The plugin provides a **pr-reviewer** subagent you can use instead of the command. Subagents run in a separate context and return results to your main conversation.

**1. Browse available agents**

Run `/agents` to open the agents interface. You'll see **pr-reviewer** listed under plugin agents (alongside built-in and custom agents). Use this to confirm it's available; you don't start a review from here.

**2. Invoke via explicit request**

In the chat, ask Claude to use the pr-reviewer agent:

```
Use the pr-reviewer agent to review this PR
```

```
Have the pr-reviewer subagent review PR #123
```

```
Use pr-reviewer to review the current branch vs main
```

You can pass context (PR number, branch name) in the same message. For fix mode, add: *"...and apply fixes"* or *"...with --fix"*.

**3. Natural language (auto-delegation)**

Claude can delegate to pr-reviewer when the task matches its description. Examples:

- *"Review this pull request"*
- *"Review PR #42"*
- *"Run a full PR review before merge"*

If Claude routes your request to pr-reviewer, you'll see it run in the subagent context.

---

## Running autonomously (Claude Code)

Claude Code prompts "Do you want to proceed?" before shell commands by default. To run PR reviews **without any human approval**:

### Option 1: Permission allow rules (recommended)

Add Bash allow rules so `gh pr`, `gh api`, `python3`, and `git` run without prompts. In your project's `.claude/settings.local.json` (or `~/.claude/settings.json` for global use):

```json
{
  "permissions": {
    "allow": [
      "Bash(gh pr *)",
      "Bash(gh api *)",
      "Bash(python3 *)",
      "Bash(git status *)",
      "Bash(git diff *)",
      "Bash(git log *)",
      "Bash(git branch *)",
      "Bash(git show *)",
      "Bash(git rev-parse *)"
    ]
  }
}
```

For `--fix` mode (apply and push changes), also add:

```json
"Bash(git add *)",
"Bash(git commit *)",
"Bash(git push *)"
```

### Option 2: bypassPermissions mode

Set `defaultMode` to skip all permission checks:

```json
{
  "defaultMode": "bypassPermissions"
}
```

Only use this in isolated environments (containers, VMs). Never on production or directories with credentials.

### Option 3: CLI flag

Launch with `--dangerously-skip-permissions`:

```bash
claude --dangerously-skip-permissions
```

Same caveats as bypassPermissions — use only in safe environments.

---

## Related setup

- **MCP & GitHub auth**: See [plugins/pr-reviewer/docs/mcp-config.md](../plugins/pr-reviewer/docs/mcp-config.md) and [plugins/pr-reviewer/docs/git-auth.md](../plugins/pr-reviewer/docs/git-auth.md).
