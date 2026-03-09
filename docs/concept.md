# Xianix Team — AI-Augmented Software Development

> Humans and AI agents, working as one team across the full SDLC.

---

## 1. Vision

Software teams today face a tension: move fast or maintain quality. Xianix Team dissolves that trade-off by embedding a coordinated set of AI agents into every phase of the software development lifecycle. The goal is **not** full automation — it is **amplification**. Human engineers operate as Forward-Deployed Engineers (FDEs) at 10x efficacy, while AI agents handle the repetitive, detail-heavy work that ensures quality, consistency, and standards are never compromised.

The agents are not isolated tools. They are **aware of each other**, aware of the **human team members**, and aware of the **product context** they operate in. They form a collaborative mesh that augments the human team rather than replacing it.

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        XIANIX TEAM                                       │
│                                                                          │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐               │
│   │   Product    │    │  Developers  │    │     QA       │               │
│   │   Owner      │    │   (FDEs)     │    │   Engineers  │               │
│   └──────┬───────┘    └──────┬───────┘    └──────┬───────┘               │
│          │                   │                   │                       │
│   ═══════╪═══════════════════╪═══════════════════╪═══════════ MESH ═══   │
│          │                   │                   │                       │
│   ┌──────▼───────┐    ┌─────▼────────┐    ┌─────▼────────┐               │
│   │  Requirement │    │  Technical   │    │   PR Review  │               │
│   │  Analyst     │    │  Design      │    │   Agent      │               │
│   │  Agent       │    │  Agent       │    │              │               │
│   └──────────────┘    └──────────────┘    └──────────────┘               │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐               │
│   │  Sprint      │    │  Test        │    │  Doc         │               │
│   │  Planning    │    │  Strategy    │    │  Maintainer  │               │
│   │  Agent       │    │  Agent       │    │  Agent       │               │
│   └──────────────┘    └──────────────┘    └──────────────┘               │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Core Principles

### 2.1 Human-First, AI-Augmented

Every decision of consequence is made or approved by a human. Agents propose, analyze, draft, and verify — humans decide, prioritize, and create. The human remains the author; the agent is the tireless co-pilot.

### 2.2 Agent Mesh — Awareness and Coordination

Agents are not siloed. Each agent is aware of:

- **Other agents** — what they produce, what they expect as input, and when to hand off.
- **Human team members** — who created a backlog item, who is assigned to a task, who to reach out to for clarification.
- **Product context** — the current architecture, existing features, coding standards, and project conventions.

This shared awareness enables a seamless flow where the output of one agent becomes the input of the next, with humans intervening at natural decision points.

### 2.3 10x FDEs, Not Replacement

The aim is to make every engineer a 10x engineer. AI handles the undifferentiated heavy lifting — analysis, boilerplate, reviews, test case design — so humans can focus on creative problem-solving, product thinking, and the judgment calls that define great software.

---

## 3. The SDLC Agent Pipeline

Each phase of the SDLC is supported by a dedicated agent. Together they form a pipeline where work flows from ideation to production with continuous quality enforcement.

```
 Backlog              Grooming         Sprint        Development     PR Review      Testing
 ──────►  RA  ──────►  Human  ──────►  TDA  ──────►  FDE + AI  ──────►  PRA  ──────►  TSA
           │           Decision          │            Tools            │              │
           │                             │                             │              │
           ▼                             ▼                             ▼              ▼
    Elaborated                         Technical Design              Review            Risk-Based
    Requirements                       & Spec                        Feedback          Test Cases

 RA  = Requirement Analyst Agent
 TDA = Technical Design Agent
 PRA = PR Review Agent
 TSA = Test Strategy Agent
```

---

### 3.1 Requirement Analyst Agent

**Trigger:** A new backlog item (story, task, bug) is created.

**What it does:**

1. **Contextual analysis** — Reads the backlog item and cross-references it against the existing product: current features, architecture, related stories, and known constraints.
2. **Requirement elaboration** — Expands terse descriptions into structured requirements with acceptance criteria, edge cases, dependencies, and assumptions.
3. **Gap identification** — Flags ambiguities, missing information, contradictions with existing functionality, or under-specified acceptance criteria.
4. **Human outreach** — When gaps are found, the agent reaches out to the person who created the backlog item (or the designated product owner) with specific, targeted questions. It does not ask vague "can you clarify?" — it asks precise questions grounded in the analysis.
5. **Iterative refinement** — Incorporates answers and re-elaborates until the item meets a defined "groomed" threshold.

**Output:** A fully elaborated backlog item with structured acceptance criteria, identified risks, dependencies, and open questions resolved.

**Human touchpoint:** The product owner or creator reviews the elaborated item and either approves it as groomed or provides additional context. The human decides when the item is ready for sprint planning.

---

### 3.2 Sprint Planning Agent

**Trigger:** A groomed backlog item is moved into a sprint by a human.

**What it does:**

1. **Capacity analysis** — Considers team velocity, current sprint load, and individual availability.
2. **Dependency mapping** — Identifies dependencies between items in the sprint and flags sequencing constraints.
3. **Risk assessment** — Highlights items with high complexity, cross-cutting concerns, or external dependencies that could block progress.
4. **Recommendation** — Suggests sprint composition adjustments if overloaded, or flags items that may not fit based on historical data.

**Output:** Sprint risk report, dependency graph, and capacity recommendations.

**Human touchpoint:** The team makes final sprint commitment decisions. The agent informs — the team decides.

---

### 3.3 Technical Design Agent

**Trigger:** A backlog item is committed to a sprint and assigned to a developer.

**What it does:**

1. **Architecture alignment** — Reviews the groomed requirements against the current system architecture (from architecture docs, code structure, and conventions).
2. **Technical specification** — Produces a technical design document covering: affected components, data model changes, API contracts, integration points, and migration considerations.
3. **Pattern enforcement** — Ensures the proposed design follows established patterns (layering, dependency direction, naming conventions) and flags deviations.
4. **Implementation guidance** — Provides a suggested implementation approach: which files to modify, what interfaces to implement, what tests to write.

**Output:** A technical design spec aligned with the architecture, ready for a developer to pick up.

**Human touchpoint:** The developer (and optionally a senior engineer) reviews the technical design. They may adjust the approach based on tacit knowledge, upcoming refactors, or pragmatic trade-offs the agent can't see. The developer owns the final design.

---

### 3.4 Development Phase — Human FDE + AI Tools

This is where the human is in the driver's seat. The developer uses AI-powered coding tools (IDE assistants, code generation, refactoring aids) to implement the feature based on the technical design spec.

The agent mesh supports this phase passively:

- The technical design spec is available as context for AI coding tools.
- Architecture rules and coding standards are embedded in the development environment.
- The developer codes at 10x speed, with AI handling boilerplate, suggesting implementations, and catching obvious errors in real time.

**Human touchpoint:** The developer writes, tests, and commits the code. They are the author.

---

### 3.5 PR Review Agent

**Trigger:** A pull request is created.

**What it does:**

1. **Functional correctness** — Reviews the PR diff against the original requirements and technical design spec to verify the implementation satisfies the acceptance criteria.
2. **Architecture fitness** — Checks adherence to the preferred architecture: layering rules, dependency direction, naming conventions, and structural patterns (see [architecture.md](architecture.md) for the existing fitness test approach).
3. **Code quality** — Evaluates readability, complexity, error handling, and adherence to project coding standards.
4. **Risk flagging** — Identifies high-risk changes (security-sensitive code, data migrations, public API changes) and escalates them for human review.
5. **Feedback** — Posts structured review comments on the PR with clear pass/fail per check, specific violations, and actionable recommendations.

**Output:** PR review with categorized findings (architecture, functional, quality, risk) posted as comments or status checks.

**Human touchpoint:** The developer addresses review feedback. A human reviewer may do a final sign-off, especially for high-risk changes flagged by the agent.

---

### 3.6 Test Strategy Agent

**Trigger:** PR review is complete and approved.

**What it does:**

1. **Risk-based test case generation** — Analyzes the changes and identifies the highest-risk areas that need manual testing. Focuses effort where it matters most rather than generating exhaustive, low-value test cases.
2. **Regression mapping** — Identifies existing functionality that could be affected by the changes and recommends targeted regression tests.
3. **Test scenario design** — Produces concrete, executable test scenarios covering happy paths, edge cases, error conditions, and integration boundaries for the risky areas.
4. **Coverage gap analysis** — Compares the change scope against existing automated test coverage and highlights gaps.

**Output:** A prioritized set of manual test cases focused on the highest-risk areas, plus recommendations for new automated tests.

**Human touchpoint:** QA engineers (or the developer) execute the manual test cases and decide whether additional exploratory testing is needed. The human applies judgment about what "feels" risky beyond what the agent identified.

---

### 3.7 Documentation Maintainer Agent

**Trigger:** Changes are merged to the main branch.

**What it does:**

1. **Impact analysis** — Determines which documentation is affected by the merged changes.
2. **Auto-update** — Updates architecture docs, API docs, component docs, and README files to reflect the new state (see [DOC_MAINTENANCE_GUIDE.md](DOC_MAINTENANCE_GUIDE.md)).
3. **Gap detection** — Identifies new features or components that lack documentation and drafts initial docs.

**Output:** Documentation PRs or updates that keep docs in sync with the codebase.

**Human touchpoint:** A team member reviews documentation changes for accuracy and tone.

---

## 4. Agent Mesh Architecture

### 4.1 Shared Context Layer

All agents operate on a shared understanding of:

| Context | Source |
|---------|--------|
| Product backlog & history | Project management tool (Jira, Azure Boards, Linear) |
| Architecture & conventions | `docs/architecture.md`, `.arch-tests/rules.md`, coding standards |
| Codebase | Git repository (full history, branches, PRs) |
| Team members & roles | Team configuration (who owns what, who to reach out to) |
| Agent outputs | Previous agent artifacts (elaborated requirements, design specs, review results) |

### 4.2 Communication Model

Agents communicate through two mechanisms:

1. **Artifact passing** — The output of one agent (e.g., elaborated requirements) becomes an input artifact for the next agent (e.g., technical design). Artifacts are stored in the project management tool or repository.
2. **Human-in-the-loop messaging** — When an agent needs human input (e.g., the Requirement Analyst needs clarification), it sends a targeted message to the relevant person through the team's communication channel (Slack, Teams, email) with specific questions and context.

### 4.3 Agent Registry

Each agent registers its capabilities, triggers, inputs, and outputs:

```
Agent Registry
├── requirement-analyst
│   ├── triggers: [backlog_item.created]
│   ├── inputs:   [backlog_item, product_context, architecture_docs]
│   ├── outputs:  [elaborated_requirement]
│   └── reaches:  [item_creator, product_owner]
├── technical-design
│   ├── triggers: [sprint_item.assigned]
│   ├── inputs:   [elaborated_requirement, architecture_docs, codebase]
│   ├── outputs:  [technical_design_spec]
│   └── reaches:  [assigned_developer, tech_lead]
├── pr-review
│   ├── triggers: [pull_request.created, pull_request.updated]
│   ├── inputs:   [pr_diff, architecture_rules, technical_design_spec, requirements]
│   ├── outputs:  [review_comments, status_checks]
│   └── reaches:  [pr_author]
├── test-strategy
│   ├── triggers: [pull_request.approved]
│   ├── inputs:   [pr_diff, requirements, existing_test_coverage]
│   ├── outputs:  [manual_test_cases, automation_recommendations]
│   └── reaches:  [qa_engineer, pr_author]
└── doc-maintainer
    ├── triggers: [branch.merged]
    ├── inputs:   [merge_diff, existing_docs]
    ├── outputs:  [doc_updates, new_doc_drafts]
    └── reaches:  [tech_lead]
```

---

## 5. How It Feels — A Day in the Life

**Morning.** The product owner creates a backlog item: *"As a user, I want to export reports as PDF."* Within minutes, the **Requirement Analyst Agent** has analyzed it against the existing reporting module, identified that the current export only supports CSV, flagged questions about page layout preferences and whether charts should be embedded, and sent those questions to the PO on Slack.

**Mid-morning.** The PO answers the questions. The agent updates the item with full acceptance criteria, edge cases (empty reports, large datasets, special characters), and dependency notes (needs a PDF rendering library). The item is marked groomed.

**Sprint planning.** The team pulls the item into the sprint and assigns it to a developer. The **Technical Design Agent** produces a spec: extend the `IReportExporter` interface, add a `PdfReportExporter` implementation using the existing renderer abstraction, add configuration for page size, and update the export endpoint. It flags that the PDF library choice should be reviewed by the tech lead.

**Afternoon.** The developer picks up the spec, opens the IDE, and starts coding. With the design spec as context, their AI coding assistant understands exactly what to build. The developer focuses on the nuanced rendering logic while AI handles the boilerplate — interface implementation, dependency injection registration, configuration binding. They finish in a fraction of the usual time.

**End of day.** The developer opens a PR. The **PR Review Agent** checks it against the architecture (correct layering? dependency direction?), against the requirements (all acceptance criteria addressed?), and against code standards. It posts a comment: architecture checks pass, but one acceptance criterion (special characters in report titles) isn't covered by the implementation. The developer adds handling and pushes.

**Next morning.** The PR is approved. The **Test Strategy Agent** identifies the highest-risk areas: PDF rendering with large datasets, special character handling, and the interaction between the new exporter and the existing CSV path. It generates targeted manual test cases for these areas. The QA engineer runs them, finds a layout issue with wide tables, and the developer fixes it.

**Merge.** The code is merged. The **Doc Maintainer Agent** updates the API documentation with the new export endpoint and adds a section to the component docs about the PDF exporter.

---

## 6. Extended Agent Roster — Future Onboarding

The core pipeline (Requirement Analyst, Technical Design, PR Review, Test Strategy, Doc Maintainer) covers the primary SDLC flow. But the mesh is designed to grow. The following agents can be onboarded to address specialized concerns that arise in real-world product teams.

### 6.1 Bug Fix Agent

**Trigger:** A bug report is filed or a production incident is created.

**What it does:**

- Analyzes the bug report against recent changes, commit history, and affected code paths to narrow down likely root causes.
- Correlates with logs, error traces, and monitoring data (if accessible) to build a causal hypothesis.
- Produces a structured investigation report: suspected root cause, affected components, reproduction steps, and a suggested fix approach.
- Links the bug to the originating PR or backlog item for traceability.

**Human touchpoint:** The developer validates the hypothesis, implements the fix, and decides on the remediation scope.

---

### 6.2 Code Health Agent

**Trigger:** Scheduled (e.g., weekly) or on-demand scan of the codebase.

**What it does:**

- Identifies code smells, dead code, unused dependencies, overly complex methods, and duplicated logic across the codebase.
- Tracks tech debt trends over time — is the codebase getting healthier or accumulating rot?
- Proposes targeted refactoring backlog items with effort estimates and risk assessments.
- Monitors dependency freshness — flags outdated packages, known vulnerabilities (CVEs), and deprecated APIs.

**Human touchpoint:** The tech lead reviews proposed refactoring items and prioritizes them against feature work. The agent informs the debt conversation — the team decides what to pay down and when.

---

### 6.3 Security Review Agent

**Trigger:** PR created (runs alongside PR Review Agent) or scheduled scan.

**What it does:**

- Scans for common vulnerability patterns: injection risks, insecure deserialization, hardcoded secrets, missing input validation, broken auth flows.
- Checks dependency trees for known CVEs against vulnerability databases.
- Reviews configuration changes for security implications (exposed ports, relaxed CORS, permissive IAM policies).
- Enforces security-specific coding standards (e.g., parameterized queries, output encoding, secure defaults).

**Human touchpoint:** Security-sensitive findings require human review before merge. The agent categorizes findings by severity so the team can triage effectively.

---

### 6.4 Performance Sentinel Agent

**Trigger:** PR created (for change-level analysis) or scheduled benchmark runs.

**What it does:**

- Analyzes code changes for performance regressions: N+1 queries, unbounded loops, missing pagination, large allocations, blocking I/O on hot paths.
- Compares benchmark results before and after changes to detect degradation.
- Reviews database query changes for missing indexes, full table scans, or inefficient joins.
- Flags API response time increases and memory usage trends from monitoring data.

**Human touchpoint:** The developer decides whether flagged patterns are acceptable trade-offs or genuine regressions that need fixing.

---

### 6.5 Release Manager Agent

**Trigger:** A release branch is cut or a release is tagged.

**What it does:**

- Compiles release notes from merged PRs, linked backlog items, and commit messages since the last release.
- Verifies release readiness: all linked items are closed, required tests have passed, no open blockers.
- Checks for breaking changes and flags them with migration guidance.
- Generates changelogs categorized by type (features, fixes, breaking changes, internal improvements).
- Coordinates deployment checklists — what config changes, migrations, or feature flags need attention.

**Human touchpoint:** The release manager reviews and approves the release. The agent assembles the paperwork — the human makes the call.

---

### 6.6 Incident Response Agent

**Trigger:** Production alert or incident ticket created.

**What it does:**

- Correlates the incident with recent deployments, config changes, and infrastructure events to identify probable causes.
- Retrieves relevant logs, error traces, and metrics from the incident timeframe.
- Suggests immediate mitigation actions (rollback candidates, feature flag toggles, traffic shifting).
- Drafts the initial incident timeline and maintains it as the response progresses.
- After resolution, generates a post-mortem draft with root cause, impact, timeline, and action items.

**Human touchpoint:** The on-call engineer drives the response. The agent accelerates diagnosis and handles the documentation burden during high-stress moments.

---

### 6.7 Onboarding Agent

**Trigger:** A new team member is added to the project.

**What it does:**

- Generates a personalized onboarding guide based on the new member's role: relevant architecture areas, key code paths, important conventions, and who to talk to.
- Provides a curated reading list of the most relevant docs, ADRs, and past design decisions.
- Answers codebase questions interactively — "Where does authentication happen?", "How do we handle migrations?", "What's the testing strategy?"
- Suggests good first issues or starter tasks calibrated to the new member's experience level.

**Human touchpoint:** A team buddy reviews the onboarding plan and adds tribal knowledge that lives outside the codebase.

---

### 6.8 API Contract Agent

**Trigger:** PR modifies API endpoints, schemas, or contracts.

**What it does:**

- Detects breaking changes in API contracts (removed fields, changed types, altered response shapes) and flags them before merge.
- Validates API changes against OpenAPI/Swagger specs and ensures specs are updated to match.
- Checks backward compatibility with known consumers.
- Enforces API design standards (naming conventions, pagination patterns, error response formats, versioning strategy).

**Human touchpoint:** The API owner reviews flagged breaking changes and decides on versioning or migration strategy.

---

### 6.9 Compliance & Governance Agent

**Trigger:** Scheduled audit or triggered by changes to sensitive areas (data handling, auth, privacy).

**What it does:**

- Monitors code changes for compliance implications — data retention, PII handling, consent flows, audit logging.
- Verifies that required controls are in place (encryption at rest, access logging, data masking).
- Maps code changes to regulatory requirements (GDPR, SOC 2, HIPAA) and flags gaps.
- Maintains an evidence trail for audits — what changed, when, who approved it, what tests covered it.

**Human touchpoint:** The compliance officer or designated team member reviews flagged items. The agent makes compliance tractable — the human ensures it's correct.

---

### 6.10 Migration Agent

**Trigger:** A migration task is created (database schema change, framework upgrade, API version bump, cloud migration).

**What it does:**

- Analyzes the migration scope: what code, configs, and data are affected.
- Generates migration scripts (schema migrations, data transforms) and validates them against test datasets.
- Identifies call sites, dependencies, and integration points that need updating.
- Produces a migration plan with sequencing, rollback steps, and validation checkpoints.

**Human touchpoint:** The developer and tech lead review the migration plan. The agent handles the tedious impact analysis — the human handles the judgment calls about timing and risk tolerance.

---

### 6.11 Roadmap Summary

| Agent | SDLC Phase | Primary Value |
|-------|-----------|---------------|
| Bug Fix | Maintenance | Accelerated root cause analysis |
| Code Health | Maintenance | Continuous tech debt visibility |
| Security Review | Build & Review | Shift-left security enforcement |
| Performance Sentinel | Build & Review | Proactive regression detection |
| Release Manager | Release | Automated release readiness and notes |
| Incident Response | Operations | Faster diagnosis and post-mortems |
| Onboarding | Team Growth | Reduced ramp-up time for new members |
| API Contract | Build & Review | Breaking change prevention |
| Compliance & Governance | All Phases | Continuous regulatory alignment |
| Migration | Maintenance | Reduced risk and effort for migrations |

All extended agents follow the same principles as the core pipeline: they plug into the agent mesh, share context, reach out to humans when needed, and never make final decisions autonomously.

---

## 7. What This Is Not

- **Not full automation.** Humans make every meaningful decision. Agents propose and verify.
- **Not a replacement for skilled engineers.** Agents amplify skill — they don't substitute for it. A 10x multiplier on zero is still zero.
- **Not a rigid pipeline.** Agents adapt to the team's workflow. Steps can be skipped, reordered, or run in parallel as the situation demands.
- **Not opaque.** Every agent action is visible, traceable, and auditable. The team always knows what the agents did and why.

---

## 8. Success Metrics

| Metric | What it measures |
|--------|-----------------|
| **Cycle time** | Time from backlog item creation to production — targeting significant reduction |
| **Defect escape rate** | Bugs found in production vs. caught during development — targeting near-zero regressions |
| **Requirements churn** | Changes to requirements after sprint commitment — lower churn = better grooming |
| **Review turnaround** | Time from PR creation to actionable feedback — targeting minutes, not hours |
| **Developer satisfaction** | Self-reported flow state and reduced toil — the human experience matters |

---

## 9. Relationship to Xianix

Xianix Team builds on the [Xianix architecture fitness test agent](architecture.md) as the foundation for the PR Review Agent, and extends the concept across the full SDLC. The shared infrastructure — Git provider abstraction, Claude CLI integration, webhook handling — is reused and extended for each new agent in the mesh.
