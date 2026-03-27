# Provider: Generic / Unknown Platform

Use this provider when:
- The git remote does not match GitHub, Azure DevOps
- `PLATFORM` is unset or unrecognised
- `JIRA_CONFLUENCE_URL` is not set (Jira fallback)
- API posting is not possible or not desired

## Behaviour

In generic mode the release notes are **not posted to a remote platform**. Instead, the compiled Markdown is written to a local file so it can be consumed by an external process, CI pipeline, or human operator.

---

## Writing the Release Note File

Write the full formatted release note to a file in the repository root:

```
release-notes-<sanitized-name>.md
```

Where `<sanitized-name>` is the sprint name or tag with spaces replaced by hyphens and special characters removed (e.g. `Sprint 42` → `release-notes-Sprint-42.md`, `v1.4.2` → `release-notes-v1.4.2.md`).

**File format:**

```markdown
# Release Notes — <sprint-name or tag>

Generated: <ISO 8601 timestamp>
Platform: <detected platform or "generic">
Reference: <sprint name | tag | milestone>
Total items: <count>
Verdict: SAVED TO FILE

---

<full formatted release note body from content-writer>
```

The file must be written even if the item count is zero — it serves as an audit artifact for the run.

---

## Output

On completion:

```
Release notes complete: <sprint-name> — <N> items — written to release-notes-<name>.md
```

---

## When to Use

This provider is the correct choice for:
- Bitbucket repositories (no release notes API implemented — use generic)
- Self-hosted GitLab instances
- Any on-premises git server
- Local or offline runs where no remote API is available
- CI environments where only the file output is needed
- Jira projects without a Confluence wiki configured
