---
name: analyze-requirement
description: Groom a GitHub issue. Analyzes user intent, domain knowledge, competitive context, and workflow. Usage: /analyze-requirement [issue-number]
argument-hint: [issue-number]
---

Perform requirement grooming for issue $ARGUMENTS.

Use the **req-analyst** agent to:

1. Fetch issue context via MCP:
   - `mcp__github__get_issue` — title, body, labels, assignee, milestone
   - `mcp__github__list_issues` — related issues by label or milestone

2. Run three analysts focused on user experience:

   **Phase 1 (parallel — context gathering):**
   - **intent-analyst** — Intent decomposition, user context, workflow, decision points
   - **domain-analyst** — Domain knowledge, data meaning, business rules, competitive insights (via web search)

   **Phase 2 (after Phase 1):**
   - **gap-risk-analyst** — Gaps, risks, value/priority, dependencies

3. Compile into a structured elaboration:
   - Summary with intent decomposition
   - User Context & Workflow
   - Domain Context
   - Gaps & Unresolved Questions
   - Risks, Dependencies & Assumptions

4. Post to GitHub automatically — no user confirmation required

5. If invoked with `--comment`: post as a comment instead of updating the issue body.

If an issue number is provided (e.g., `/analyze-requirement 42`), fetch the issue details first.

If no argument is given, prompt the user for an issue number.
