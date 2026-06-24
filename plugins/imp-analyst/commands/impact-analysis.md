---
name: impact-analysis
description: Analyze a PR for functional impact and produce a QA-focused risk report. Identifies high-risk areas, blast radius, regression risk, and recommended test plan. Works with GitHub, Azure DevOps, and any git repository. Usage: /impact-analysis [PR number, branch name, or leave blank for current branch]
argument-hint: [pr-number | branch-name]
---

Run a comprehensive impact analysis for $ARGUMENTS.

## What This Does

This command invokes the **imp-analyst** agent which orchestrates four specialized analysts:

| Analyst | Focus |
|---------|-------|
| `change-scope-analyzer` | Categorizes all changes: new code, modified logic, deletions, config, schema |
| `dependency-tracer` | Traces callers, callees, data flows, and blast radius from changed code |
| `feature-mapper` | Maps code changes to user-facing features and business workflows |
| `risk-assessor` | Rates risk per area, assesses regression likelihood, recommends test focus |

## How to Use

```
/impact-analysis              # Analyze current branch vs main
/impact-analysis 123          # Analyze PR #123
/impact-analysis feature/foo  # Analyze branch feature/foo vs main
```

## Platform Support

The plugin auto-detects the hosting platform from your git remote URL:

| Remote URL contains | Platform | How report is posted |
|---|---|---|
| `github.com` | GitHub | GitHub MCP server or `gh` CLI (PR comment) |
| `dev.azure.com` / `visualstudio.com` | Azure DevOps | `curl` with `AZURE_TOKEN` (PR thread) |
| Anything else | Generic | Written to `impact-analysis-report.md` |

All diff and file content gathering uses standard git commands — no platform-specific API is needed for the analysis phase.

## Output

The analysis produces a structured impact report:

```
## Impact Analysis Report
Risk Level: 🔴 HIGH | 🟡 MEDIUM | 🟢 LOW

### Executive Summary
### High-Risk Areas (Priority Testing Required)
### Impacted Features
### Blast Radius
### Regression Risk
### Recommended Test Plan
### Safe Areas (Low Risk)
```

## After the Analysis

The report is posted to your platform automatically as part of this command — no further steps required. The agent will output a single confirmation line:

**GitHub:**
```
Impact analysis posted on PR #<number>: <risk-level> — <N> high-risk areas — <URL>
```

**Azure DevOps:**
```
Impact analysis posted on PR #<number>: <risk-level> — <N> high-risk areas — <URL>
```

**Generic / unknown platform:**
```
Impact analysis complete: <risk-level> — report written to impact-analysis-report.md
```

## Prerequisites

- Must be run inside a git repository
- The current branch must have at least one commit ahead of the base branch
- **GitHub**: GitHub MCP server connected or `gh` CLI installed
- **Azure DevOps**: `AZURE_TOKEN` environment variable set with appropriate scopes

---

Starting impact analysis now...
