# Backlog Setup — Using GitHub Issues

The `req-analyst` plugin uses GitHub Issues as the backlog source. This guide explains how to structure your issues for best results with the agent.

---

## Issue Structure

The agent works with any issue format, but produces better elaborations when the original issue includes:

### Minimum (title only)
The agent can elaborate from just a title, but will flag many gaps:
```
Title: Add user profile page
Body: (empty)
```

### Recommended (title + description)
Include a brief description of the intent and any known constraints:
```
Title: Add user profile page
Body:
Users should be able to view and edit their profile information
including name, email, and avatar. This is part of the
user management epic.
```

### Ideal (title + description + context)
Include any existing acceptance criteria, references, or constraints:
```
Title: Add user profile page
Body:
Users should be able to view and edit their profile information.

## Context
- Part of epic #15 (User Management)
- Design mockup: [link]
- API endpoint already exists: GET /api/users/:id

## Initial Acceptance Criteria
- User can view their profile
- User can update their name and email
- Avatar upload supports JPG and PNG
```

---

## Labels

The agent applies these labels automatically based on its analysis:

| Label | Meaning |
|---|---|
| `groomed` | Fully elaborated, ready for sprint planning |
| `needs-clarification` | Questions posted, awaiting product owner response |
| `needs-decomposition` | Too large, should be split into smaller items |

You may want to create these labels in your repository beforehand. The agent will create them if they don't exist (requires repo admin permissions).

---

## Issue Types

The agent auto-detects the item type from labels or content:

| Type | Detection | Elaboration Focus |
|---|---|---|
| **Story** | Label `story` or `feature`, or describes user behavior | User-facing AC, personas, UI/UX edge cases |
| **Task** | Label `task` or `chore`, or describes technical work | Technical AC, infrastructure dependencies |
| **Bug** | Label `bug`, or describes broken behavior | Reproduction steps, expected vs actual, regression AC |
| **Spike** | Label `spike` or `research`, or describes investigation | Research questions, success criteria, time-box |

---

## Workflow

1. Create a GitHub issue with at least a title and brief description
2. Run `/requirement-analysis <issue-number>`
3. The agent elaborates the issue and posts the result
4. Product owner reviews the elaboration:
   - If `GROOMED` — move to sprint planning
   - If `NEEDS CLARIFICATION` — answer the posted questions, then re-run
   - If `NEEDS DECOMPOSITION` — split into smaller issues and elaborate each

---

## Future Sources

This plugin currently supports GitHub Issues. Support for Jira and Azure DevOps Boards is planned for a future release. The agent's sub-agents are source-agnostic — only the orchestrator's fetch and post steps need to be adapted for new sources.
