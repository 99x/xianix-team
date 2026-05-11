# Security Report Style Guide

## Core Principles

- **Signal over noise** — only report real, exploitable issues in changed lines
- **Specific over vague** — name the file, line, and variable; show a fix snippet
- **Proportionate** — a small config change should not produce a long report
- **Actionable** — every finding must include a clear recommendation

## Severity Levels

| Level | Meaning | Action |
|---|---|---|
| `CRITICAL` | Exploitable with no prerequisites (hardcoded secrets, unauthenticated RCE) | Block merge immediately |
| `HIGH` | Exploitable with low effort (SQLi, auth bypass, XSS with user data) | Block merge |
| `MEDIUM` | Exploitable under specific conditions or requiring chaining | Fix before merge (recommended) |
| `LOW` | Defense-in-depth improvement, unlikely to be exploited alone | Fix when convenient |
| `INFO` | Observation only — no exploitability (placeholder-like values, minor patterns) | No action required |

## Verdict Labels

| Verdict | When to Use |
|---|---|
| `APPROVED` | No findings or INFO-only observations |
| `APPROVED WITH SUGGESTIONS` | MEDIUM or LOW findings only |
| `CHANGES REQUESTED` | Any HIGH or CRITICAL finding |

## Tone

- Direct and technical — write for a developer, not a compliance auditor
- Explain the impact briefly ("this allows an attacker to...") before the recommendation
- Do not use filler phrases like "please consider" or "it would be advisable"
- Do not repeat the same recommendation across multiple findings — consolidate where possible
