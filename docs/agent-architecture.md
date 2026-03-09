# Agent Architecture

## Setup

When the agents first activated on the tenant

- Obtain the GIT_SECRET and save it in configuration and pass as a workflow parameter
- Agent checks if the .xianix folder is available. If not setup and push to the master.
- All Clude Code subagents will be dowoloaded as plugins via xianix public GitHub repo
- Webhook is used to listen to both GitHub and AzureDevops pull_request.created and pull_request.synchronized events

## PR Review

- Agent is able to distinguish and extreact information from GitHub and AzureDevops
- If the code branch is not locally present, git subagent checkout the code
- Invoke the PR review subagent to do the review

## Cloude Code

Each Agent is built on top of a set of Claude Code plugins. These plugins will represent different job-roles in software engineering.
