---
name: analyze-requirement
description: Elaborate a GitHub issue into a fully groomed backlog item. Runs codebase context, acceptance criteria, dependency, and gap analysis. Usage: /analyze-requirement [issue-number]
argument-hint: [issue-number]
---

Perform a comprehensive requirement elaboration for issue $ARGUMENTS.

Use the **requirement-analyst** agent to:

1. Fetch issue context via MCP (always fresh):
   - `mcp__github__get_issue` — title, body, labels, assignee, milestone
   - `mcp__github__list_issues` — related issues by label or milestone
   - `mcp__github__get_file_contents` — codebase files for architectural context

2. Run specialized sub-agent analyses in parallel:
   - **context-analyst** — Affected modules, related issues, existing patterns, architectural context
   - **acceptance-criteria-writer** — Structured Given/When/Then criteria, edge cases, boundary conditions
   - **dependency-analyzer** — Upstream/downstream dependencies, risks, constraints, assumptions
   - **gap-detector** — Ambiguities, missing information, contradictions, under-specification

3. Compile all findings into a single structured elaboration with:
   - Overall verdict: `GROOMED`, `NEEDS CLARIFICATION`, or `NEEDS DECOMPOSITION`
   - Testable acceptance criteria
   - Dependencies and risks tables
   - Unresolved questions with suggested clarifying questions

4. Post the elaboration to GitHub automatically — no user confirmation required:
   - Use `mcp__github__update_issue` to replace the issue body with the elaborated requirement
   - Use `mcp__github__add_labels_to_issue` to apply the appropriate status label
   - Use `mcp__github__create_issue_comment` for unresolved questions tagging relevant people

5. If invoked with `--comment`: post as a comment instead of updating the issue body.

If an issue number is provided (e.g., `/analyze-requirement 42`), fetch the issue details via `mcp__github__get_issue` first.

If no argument is given, prompt the user for an issue number.
