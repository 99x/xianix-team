# Requirement Elaboration Output Style Guide

This file defines the formatting and tone conventions for all output produced by the `requirement-analyst` plugin agents.

---

## General Principles

- Be **specific and grounded** — every gap, dependency, or risk must reference the actual issue content
- Be **actionable** — every unresolved question must be answerable with a yes/no or a concrete decision
- Be **proportionate** — a one-line bug fix should not produce a 500-line elaboration
- Be **constructive** — the goal is to improve the requirement, not criticize the author
- Avoid filler phrases: "Great requirement!", "This is interesting", "As an AI..."

---

## Severity Levels

Use these labels consistently across all agents:

| Label | When to use |
|---|---|
| `CRITICAL` | Blocks implementation — cannot start work without resolution |
| `WARNING` | Should be clarified before sprint — developer will guess without it |
| `INFO` | Nice to resolve — improves quality but doesn't block implementation |

---

## Verdict Labels

The final elaboration verdict must be one of exactly three values, rendered as inline code:

| Verdict | Meaning |
|---|---|
| `GROOMED` | All AC are testable, no CRITICAL gaps, dependencies identified, ready for sprint planning |
| `NEEDS CLARIFICATION` | One or more CRITICAL or WARNING gaps remain, or unresolved questions block implementation |
| `NEEDS DECOMPOSITION` | Item is too large — spans multiple domains, too many AC, or estimated as high complexity |

---

## Acceptance Criteria Format

Every acceptance criterion must be testable. Use one of these formats:

**Given/When/Then (preferred for user-facing behavior):**
```
Given [precondition or context],
when [action or trigger],
then [expected observable result].
```

**Testable condition (for technical or non-UI items):**
```
[Component/system] must [specific measurable behavior] when [condition].
```

**Anti-patterns to avoid:**
- "Should work correctly" — what does "correctly" mean?
- "Handle errors appropriately" — what is "appropriate"?
- "Be performant" — how fast exactly?
- Compound criteria testing multiple behaviors in one AC

---

## Gap Format

Every gap must follow this structure:

```
- **[Short title]** — [Description of what is ambiguous, missing, or contradictory]
  **Suggested question:** "[Precise question grounded in the analysis — not 'can you clarify?']"
```

- Reference the specific part of the issue that triggered the gap
- The suggested question must be answerable — avoid open-ended "tell me more" questions
- Tag the appropriate person (@creator, @product-owner) when posting to GitHub

---

## Section Order

The compiled elaborated requirement must follow this section order:

1. Header (Issue number, title, type, verdict)
2. Summary (3-5 sentences)
3. Acceptance Criteria (checkbox list)
4. Edge Cases
5. Dependencies (table)
6. Risks & Constraints (table)
7. Assumptions
8. Unresolved Questions
9. Architecture Notes

Do not reorder or omit sections. If a section has no findings, write:
> *No [dependencies / risks / edge cases / unresolved questions] identified.*

---

## Risk Rating

Use these indicators in the Risks table:

| Likelihood/Impact | Low | Medium | High |
|---|---|---|---|
| Description | Unlikely or minor | Possible and noticeable | Likely or significant |

---

## Dependency Types

| Type | Meaning |
|---|---|
| Upstream | Must be completed before this item can start |
| Downstream | Will be affected by this item's completion |
| External | Third-party service, API, or resource outside the team's control |

---

## Tone

- Use **neutral, professional language** — this is a technical document, not a review
- Address gaps as observations, not criticisms: "The error handling behavior is not specified" not "You forgot to specify error handling"
- Be concise — each section should be scannable in under 30 seconds
- Use bullet points over paragraphs where possible
- Questions should be precise: "Should the endpoint return 404 or 200 with an empty array when no results match?" not "What should happen when there are no results?"
