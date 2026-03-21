---
name: requirement-analysis
description: Groom a backlog item. Analyzes user intent, domain knowledge, competitive context, and workflow. Usage: /requirement-analysis [issue-number]
argument-hint: [issue-number]
---

Groom and analyze the backlog item $ARGUMENTS.

## What This Does

This command invokes the **req-analyst** agent which orchestrates three analysts focused entirely on **user experience and intent**:

**Phase 1 — Context Gathering (parallel):**

| Analyst | Focus |
|---------|-------|
| `intent-analyst` | Intent decomposition, user context, workflow, decision points |
| `domain-analyst` | Domain knowledge, data meaning, business rules, competitive insights |

**Phase 2 — Gap & Risk Analysis:**

| Analyst | Focus |
|---------|-------|
| `gap-risk-analyst` | Gaps, risks, value/priority, dependencies |

## How to Use

```
/requirement-analysis 42          # Groom GitHub issue #42
/requirement-analysis 42 --comment  # Post as comment instead of updating issue body
```

## Output

```
## Elaborated Requirement
Verdict: GROOMED | NEEDS CLARIFICATION | NEEDS DECOMPOSITION

### Summary (with intent decomposition)
### User Context & Workflow
### Domain Context
### Gaps & Unresolved Questions
### Risks, Dependencies & Assumptions
```

## After the Elaboration

The elaborated requirement is posted to GitHub automatically. The agent outputs:

```
Elaboration posted on issue #<number>: <verdict> — <N> unresolved questions
```

## Prerequisites

- GitHub MCP server must be connected (see `docs/mcp-config.md`)
- DuckDuckGo MCP for competitive/domain research (no API key — see `docs/mcp-config.md`)

---

Starting elaboration now...
