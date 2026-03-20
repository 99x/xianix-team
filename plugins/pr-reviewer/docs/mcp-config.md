# MCP Configuration

> **This document has been superseded.** Platform setup (GitHub MCP, Azure DevOps, and others) is now documented in [`docs/platform-setup.md`](./platform-setup.md).

The GitHub MCP server is now **optional** — the plugin uses git commands for all diff analysis and only uses MCP (or the `gh` CLI) for posting review comments back to GitHub.

See [`docs/platform-setup.md`](./platform-setup.md) for:
- GitHub MCP server setup
- `gh` CLI setup (GitHub fallback)
- Azure DevOps CLI setup
- Token scopes and environment variables
