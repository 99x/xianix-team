namespace AgentTeam.Console.Workflows;

/// <summary>
/// Input for the requirement analysis workflow/activity.
/// Maps to run-requirement-analysis environment: PLATFORM, REPO_URL, ISSUE_NUMBER.
/// TenantId isolates workflow IDs when multiple tenants share the same process.
/// </summary>
public sealed record RequirementAnalysisInput(
    string PlatformName,
    string RepoUrl,
    int IssueNumber,
    string TenantId
);
