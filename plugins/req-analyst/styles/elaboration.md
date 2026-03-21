# Requirement Elaboration Output Style Guide

Formatting and tone conventions for all `req-analyst` plugin output.

---

## Core Mental Model: 4 Lenses

Push every requirement through these lenses:

| Lens | Question |
|---|---|
| **Intent** | Why does this exist? What problem disappears if solved? |
| **Context** | Where/how is it used? Who? Constraints? |
| **Behavior** | What actually happens? Happy path, edge flows, failure modes? |
| **Impact** | What changes if we get it right? Wrong? Value, risks? |

This plugin focuses on **user experience** — intent, context, domain, and risk. Technical design and acceptance criteria writing are separate activities.

---

## General Principles

- **User experience first** — understand why and how before any technical considerations
- Be **specific and grounded** — every gap, risk, or dependency must reference the actual issue content
- Be **actionable** — every question must be answerable with a yes/no or concrete decision
- Be **proportionate** — a bug fix should not produce a 500-line elaboration
- Be **concise** — each section must be scannable in under 30 seconds; skip sections with no findings
- Bring **domain knowledge** — enrich, don't just restate
- Avoid filler: "Great requirement!", "This is interesting", "As an AI..."

---

## Severity Levels

| Label | When to use |
|---|---|
| `CRITICAL` | Blocks understanding — cannot groom without resolution |
| `WARNING` | Should be clarified before sprint — team will guess |
| `INFO` | Improves quality but doesn't block |

---

## Verdict Labels

| Verdict | Meaning |
|---|---|
| `GROOMED` | Intent clear; no CRITICAL gaps; value/priority understood; user context and workflow defined |
| `NEEDS CLARIFICATION` | CRITICAL or WARNING gaps remain; intent ambiguous; unresolved questions block understanding |
| `NEEDS DECOMPOSITION` | Too large — spans multiple domains, too many open dimensions |

---

## Gap Format

```
| # | Gap / Question | Severity | Suggested question |
|---|---|---|---|
| 1 | [What's ambiguous — reference specific issue content] | CRITICAL | [Precise question] — @[person] |
```

- Questions must be answerable — avoid open-ended "tell me more"
- Tag the appropriate person when posting

---

## Section Order

1. Header (Issue number, title, type, verdict)
2. Summary (with intent decomposition table)
3. User Context & Workflow
4. Domain Context
5. Gaps & Unresolved Questions
6. Risks, Dependencies & Assumptions

**Skip sections with no findings** rather than writing "None identified."

---

## Risk Rating

| Level | Meaning |
|---|---|
| Low | Unlikely or minor |
| Medium | Possible and noticeable |
| High | Likely or significant |

---

## Tone

- **Neutral, professional** — grooming document, not a review
- Gaps are observations: "Error handling is not specified" not "You forgot error handling"
- Concise — bullet points over paragraphs
- Questions must be precise: "Should the system prevent duplicate entries or allow them?" not "What about duplicates?"
