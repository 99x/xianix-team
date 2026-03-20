---
name: requirement-analysis
description: Elaborate a backlog item. Analyzes codebase context, writes acceptance criteria, identifies dependencies, and detects gaps. Usage: /requirement-analysis [issue-number]
argument-hint: [issue-number]
---

Elaborate and analyze the backlog item $ARGUMENTS.

## What This Does

This command invokes the **requirement-analyst** agent which orchestrates five specialized analysts:

| Analyst | Focus |
|---------|-------|
| `context-analyst` | Codebase context, affected modules, related issues, existing patterns |
| `acceptance-criteria-writer` | Structured acceptance criteria, edge cases, boundary conditions |
| `dependency-analyzer` | Upstream/downstream dependencies, risks, constraints, assumptions |
| `gap-detector` | Ambiguities, missing information, contradictions, under-specification |
| `competitive-context-analyst` | Similar implementations, competitor approaches (via web search) |

## How to Use

```
/requirement-analysis 42          # Elaborate GitHub issue #42
/requirement-analysis 42 --comment  # Post as comment instead of updating issue body
```

## Output

The elaboration produces a structured requirement:

```
## Elaborated Requirement
Verdict: GROOMED | NEEDS CLARIFICATION | NEEDS DECOMPOSITION

### Summary
### Acceptance Criteria
### Edge Cases
### Dependencies
### Risks & Constraints
### Assumptions
### Unresolved Questions
### Architecture Notes
### Competitive & Market Context
```

## After the Elaboration

The elaborated requirement is posted to GitHub automatically as part of this command — no further steps required. The agent will output a single confirmation line:

```
Elaboration posted on issue #<number>: <verdict> — <N> acceptance criteria — <N> unresolved questions
```

To post as a comment instead of updating the issue body:
```
/requirement-analysis 42 --comment
```

## Prerequisites

- Must be run inside a git repository (for codebase analysis)
- GitHub MCP server must be connected (see `docs/mcp-config.md`)
- Repo must have a GitHub remote

---

Starting elaboration now...
