# Webhook Provider Design

Design for identifying webhook source (GitHub vs Azure DevOps), extracting unified PR context, and handling different event types (PR created vs synchronized).

---

## 1. Goals

| Goal | Description |
|------|-------------|
| **Provider identification** | Determine if webhook is from GitHub or Azure DevOps before parsing |
| **Unified extraction** | Extract `PLATFORM`, `REPO_URL`, `PR_NUMBER` (and related fields) into a single model |
| **Event type awareness** | Distinguish `pull_request.created` from `pull_request.synchronized` |
| **Extensibility** | Easy to add future providers (e.g., GitLab) |

---

## 2. Extracted Parameters (Unified Model)

The run-pr-review scripts expect these environment variables; the webhook handler must produce equivalent data:

| Parameter | Description | Example |
|-----------|-------------|---------|
| `PLATFORM` | `github` or `azure-devops` | `github` |
| `REPO_URL` | Clone URL for the repository | `https://github.com/XiansAiPlatform/XiansAi.Server.git` |
| `PR_NUMBER` | Pull request identifier | `373` |

Additional fields from [agent-architecture.md](agent-architecture.md) that downstream workflows may need:

| Field | Description |
|-------|-------------|
| `SourceBranch` | Head/source branch ref |
| `TargetBranch` | Base/target branch ref |
| `DiffUrl` | URL to fetch diff (if available) |
| `EventType` | `PullRequestCreated` \| `PullRequestSynchronized` |

---

## 3. Class Structure

### 3.1 Core Models

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         UNIFIED PR CONTEXT                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│  PrWebhookContext                                                           │
│  ├─ Platform: GitProvider (enum: GitHub | AzureDevOps)                       │
│  ├─ EventType: PrWebhookEvent (enum: PullRequestCreated | PullRequestSync)  │
│  ├─ RepoUrl: string                                                         │
│  ├─ PrNumber: int                                                           │
│  ├─ SourceBranch: string?                                                   │
│  ├─ TargetBranch: string?                                                   │
│  └─ RawPayload: object? (optional, for debugging or fallback)               │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Provider Model (Strategy Pattern)

```
                    ┌──────────────────────────┐
                    │  IWebhookPayloadParser   │
                    ├──────────────────────────┤
                    │ + CanParse(raw): bool    │
                    │ + Parse(raw): PrWebhook  │
                    │   Context?               │
                    │ + EventType(raw):        │
                    │   PrWebhookEvent?        │
                    └────────────┬─────────────┘
                                 │
              ┌──────────────────┼──────────────────┐
              │                  │                  │
              ▼                  ▼                  ▼
┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐
│ GitHubWebhookParser │  │ AzureDevOpsWebhook   │  │ (Future: GitLab)    │
│                     │  │ Parser               │  │                     │
└─────────────────────┘  └─────────────────────┘  └─────────────────────┘
```

### 3.3 Parser Registry / Resolver

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  WebhookParserResolver                                                       │
│  ├─ _parsers: IReadOnlyList<IWebhookPayloadParser>                          │
│  ├─ TryResolve(rawPayload, headers?): IWebhookPayloadParser?                │
│  └─ Parse(rawPayload, headers?): PrWebhookContext?                          │
└─────────────────────────────────────────────────────────────────────────────┘
```

The resolver iterates over registered parsers, calls `CanParse(raw, headers)` on each, and uses the first match. This allows identification by **headers** (e.g., `X-GitHub-Event`) or **payload structure** (e.g., Azure DevOps `eventType` field).

---

## 4. Identification Strategy

Provider identification can use one or both of:

### Option A: Headers (when available)

| Provider | Header | Example value |
|----------|--------|---------------|
| GitHub | `X-GitHub-Event` | `pull_request` |
| GitHub | `X-GitHub-Delivery` | (UUID, presence indicates GitHub) |
| Azure DevOps | `Request-Context` or custom | (TBD from payload) |

### Option B: Payload Structure (when headers are opaque or unavailable)

| Provider | Discriminator | Example |
|----------|---------------|---------|
| GitHub | `pull_request` object, `repository.full_name` | Top-level `pull_request` + `repository` |
| Azure DevOps | `eventType`, `resource.repository` | `eventType: "git.pullrequest.created"` |

The resolver should try headers first (fast path), then fall back to payload inspection.

---

## 5. Event Type Mapping

| Provider | PR Created | PR Synchronized |
|----------|------------|-----------------|
| **GitHub** | `action: "opened"` | `action: "synchronize"` |
| **Azure DevOps** | `eventType: "git.pullrequest.created"` | `eventType: "git.pullrequest.updated"` + status change? |

*Exact payload paths to be confirmed when sample payloads are provided.*

---

## 6. Extraction Paths (Placeholder)

Based on [agent-architecture.md](agent-architecture.md). **To be validated against actual payloads.**

### GitHub (validated)

| Field | Path | Notes |
|-------|------|-------|
| RepoUrl | `repository.clone_url` or `repository.html_url` + `.git` | Prefer clone_url |
| PrNumber | `pull_request.number` | |
| SourceBranch | `pull_request.head.ref` | |
| TargetBranch | `pull_request.base.ref` | |
| DiffUrl | `pull_request.diff_url` | |
| EventType | `action` → `opened` = Created, `synchronize` = Synchronized | |

### Azure DevOps

| Field | Path | Notes |
|-------|------|-------|
| RepoUrl | Build from `resource.repository.remoteUrl` or org/project/repo | May need to normalize to clone URL |
| PrNumber | `resource.pullRequestId` | |
| SourceBranch | `resource.sourceRefName` (e.g., `refs/heads/feature-x`) | Strip `refs/heads/` |
| TargetBranch | `resource.targetRefName` | Strip `refs/heads/` |
| EventType | `eventType` + `resource.status`? | created vs updated |

---

## 7. Project Layout

```
AgentTeam.Console/
├── Program.cs
└── Webhooks/
    ├── Models/
    │   ├── PrWebhookContext.cs      # Unified extracted context
    │   ├── GitProvider.cs           # enum: GitHub, AzureDevOps
    │   └── PrWebhookEvent.cs        # enum: PullRequestCreated, PullRequestSynchronized
    ├── Parsers/
    │   ├── IWebhookPayloadParser.cs
    │   ├── GitHubWebhookParser.cs
    │   └── AzureDevOpsWebhookParser.cs
    └── WebhookParserResolver.cs
```

---

## 8. Usage in Program.cs

```csharp
var resolver = new WebhookParserResolver(
    new GitHubWebhookParser(),
    new AzureDevOpsWebhookParser()
);

prReviewWorkflow.OnWebhook(async (context) =>
{
    var rawPayload = context.Webhook.Payload;  // string or JsonElement?
    var headers = context.Webhook.Headers;      // if available
    
    var prContext = resolver.Parse(rawPayload, headers);
    if (prContext is null)
    {
        Console.WriteLine("Unrecognized webhook provider or invalid payload");
        return;
    }

    Console.WriteLine($"Provider: {prContext.Platform}");
    Console.WriteLine($"Event: {prContext.EventType}");
    Console.WriteLine($"Repo: {prContext.RepoUrl}");
    Console.WriteLine($"PR: #{prContext.PrNumber}");

    // Pass to run-pr-review or downstream workflow
    Environment.SetEnvironmentVariable("PLATFORM", prContext.Platform.ToString().ToLowerInvariant());
    Environment.SetEnvironmentVariable("REPO_URL", prContext.RepoUrl);
    Environment.SetEnvironmentVariable("PR_NUMBER", prContext.PrNumber.ToString());

    // ... invoke PR review workflow
});
```

---

## 9. Open Points (for payload validation)

1. **Webhook payload type**: Is `context.Webhook.Payload` a `string`, `JsonElement`, or `object`?
2. **Headers availability**: Does the webhook context expose headers (e.g., `X-GitHub-Event`)?
3. **Azure DevOps event types**: Exact values for created vs synchronized (e.g., `git.pullrequest.created` vs `git.pullrequest.updated` with merge status).
4. **Azure DevOps `remoteUrl`**: Format and whether it's the clone URL or web URL.

Once sample payloads are provided, the extraction paths in §6 can be finalized and implemented.
