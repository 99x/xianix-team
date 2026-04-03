namespace AgentTeam.Console.Workflows;

/// <summary>
/// Input for the impact analysis workflow/activity.
/// Maps to run-impact-analysis environment: PLATFORM, REPO_URL, PR_NUMBER, PR_SOURCE_REF.
/// </summary>
public sealed record ImpactAnalysisInput(
    string PlatformName,
    string RepoUrl,
    int PrNumber,
    string? SourceRef = null
);
