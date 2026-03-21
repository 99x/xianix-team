namespace AgentTeam.Console.Workflows;

/// <summary>
/// Input for the PR review script workflow/activity.
/// Maps to run-pr-review environment: PLATFORM, REPO_URL, PR_NUMBER, PR_SOURCE_REF.
/// TenantId enables directory and workflow ID isolation when multiple tenants share the same process.
/// </summary>
public sealed record PrReviewScriptInput(
    string PlatformName,
    string RepoUrl,
    int PrNumber,
    string? SourceRef = null,
    string? TenantId = null
);
