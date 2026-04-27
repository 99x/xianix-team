# Release Note Output Style Guide

This file defines the formatting and tone conventions for all output produced by the `release-note-generator` plugin agents.

---

## General Principles

- Be **user-facing** — release notes are read by stakeholders, not engineers; avoid technical jargon
- Be **concise** — titles are verbatim from work items; do not rephrase or summarise
- Be **accurate** — never infer or fabricate item counts, states, or assignees
- Be **consistent** — use the same section order, emoji, and table format every time
- Avoid filler: no "Great news!", "We're excited to announce", or "As an AI..."

---

## Emoji Scheme

Use these emoji consistently — never substitute or add others:

| Type | Emoji | Section heading |
|---|---|---|
| Feature | 🚀 | Features |
| User Story | 📖 | User Stories |
| Bug Fix | 🐛 | Bug Fixes |
| Task | ✅ | Tasks |
| Epic | 🏔️ | Epics |

---

## Section Order

The release note must always follow this order:

1. Header (title + generated date)
2. Summary table (all types, even if zero)
3. Features (if any)
4. User Stories (if any)
5. Bug Fixes (if any)
6. Tasks (if any)
7. Epics (if any)
8. Footer

Do not reorder sections. Skip sections with zero items entirely — do not include empty headings.

---

## Header Format

```markdown
## Release Notes — <sprint-name or tag>

*Generated: <ISO 8601 date>*

---
```

---

## Summary Table Format

Always include all five types in the summary table, even if zero:

```markdown
| Type | Count |
|---|---|
| 🚀 Features | 3 |
| 📖 User Stories | 8 |
| 🐛 Bug Fixes | 5 |
| ✅ Tasks | 4 |
| 🏔️ Epics | 0 |

**Total: 20 items**
```

---

## Per-Type Section Format

```markdown
### 🚀 Features

| ID | Title | State | Assigned To |
|---|---|---|---|
| [#1234](https://...) | Add export to PDF | Done | Alice Smith |
| [#1238](https://...) | Dark mode toggle | Done | Bob Jones |
```

Rules:
- `ID` column: always a Markdown hyperlink — `[#ID](url)` for Azure DevOps / GitHub / Jira
- `Title`: verbatim from the work item — do not rephrase or truncate
- `State`: verbatim from the work item (e.g. Done, Closed, Resolved)
- `Assigned To`: full name as returned by the API; use `Unassigned` if empty

---

## Footer Format

```markdown
---

*This release note was generated automatically.*
```

---

## Verdict Labels

The final release note must carry one of these verdict values (used by the orchestrator for logging):

| Verdict | Meaning |
|---|---|
| `PUBLISHED` | Successfully posted to the target platform |
| `PREVIEW` | Generated and displayed only — not posted (`--preview` flag) |
| `SAVED TO FILE` | Written to a local Markdown file (generic provider) |

---

## Tone

- Write for a business stakeholder audience — not for developers
- Use **past tense** for all items: work is done, not in progress
- Keep the document scannable — tables over prose wherever possible
- Do not add commentary, opinions, or recommendations beyond the data
