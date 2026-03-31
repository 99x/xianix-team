namespace AgentTeam.Console.Workflows;

/// <summary>
/// Input for the security review workflow/activity.
/// Maps to run-security-review environment: PLATFORM, REPO_URL, PR_NUMBER, PR_SOURCE_REF.
/// TenantId enables workflow ID isolation when multiple tenants share the same process.
/// </summary>
public sealed record SecurityReviewInput(
    string PlatformName,
    string RepoUrl,
    int PrNumber,
    string TenantId,
    string? SourceRef = null
);
