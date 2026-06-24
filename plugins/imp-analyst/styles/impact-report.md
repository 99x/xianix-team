# Impact Analysis Output Style Guide

This file defines the formatting and tone conventions for all output produced by the `imp-analyst` plugin agents.

---

## General Principles

- Be **QA-focused** — every finding should help QA decide what to test and how to prioritize
- Be **specific** — reference file paths, function names, and concrete user scenarios
- Be **actionable** — every high-risk area must include a specific test recommendation
- Be **proportionate** — don't mark everything as high risk; help QA focus on what truly matters
- Be **balanced** — always include "Safe Areas" so QA knows what to deprioritize
- Avoid filler phrases: "Great job!", "This is interesting", "As an AI..."

---

## Risk Levels

Use these levels consistently across all agents:

| Level | Emoji | When to use |
|---|---|---|
| HIGH | 🔴 | Auth, payments, DB schema, public APIs, shared core modules, no test coverage |
| MEDIUM | 🟡 | Business logic, integrations, moderate blast radius, partial test coverage |
| LOW | 🟢 | Utilities, config, docs, tests, formatting, isolated changes with good coverage |

---

## Finding Format

Every impacted area must follow this structure:

```
| [Feature/Area] | [🔴/🟡/🟢] | [Why this risk level] | [Specific test to run] |
```

- Area name should be user-understandable (e.g., "User login flow" not "src/auth/login.ts")
- Risk reason must be concrete (e.g., "login validation logic changed, no tests updated" not "important code")
- Test recommendation must be actionable (e.g., "Verify login with valid/invalid credentials, check session token generation" not "test login")

---

## Test Plan Format

Test scenarios must be specific enough for a QA engineer to execute without reading the code:

```
- [ ] **P0:** Verify that [user action] produces [expected result] when [condition]
- [ ] **P1:** Test [scenario] with [specific input] — expected: [output]
```

**Bad examples:**
- "Test the login feature" (too vague)
- "Make sure it works" (not actionable)

**Good examples:**
- "Verify login with valid email/password returns 200 and sets session cookie"
- "Verify login with expired password triggers password reset flow"
- "Verify concurrent login from two devices does not corrupt session state"

---

## Section Order

The compiled impact report must follow this section order:

1. Header (PR title, author, file counts, overall risk level)
2. Executive Summary (2-3 sentences for QA lead)
3. High-Risk Areas (table with test recommendations)
4. Impacted Features (code → user feature mapping)
5. Blast Radius (direct, dependent, indirect)
6. Regression Risk (what might break)
7. Recommended Test Plan (P0/P1/P2 prioritized)
8. Safe Areas (what QA can deprioritize)
9. Change Summary by Category (new/modified/config/schema/test/docs)

Do not reorder or omit sections. If a section has no findings, write a brief note:
> *No [high-risk areas / regression risks / etc.] identified.*

---

## Tone

- Use **third person** when describing impact: "This change affects…", "The login flow is impacted…"
- Use **imperative** for test recommendations: "Verify that…", "Test with…", "Confirm that…"
- Be concise — a finding should rarely exceed 3 lines of prose
- Focus on **what QA needs to do**, not what the developer did
