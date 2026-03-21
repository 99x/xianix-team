namespace AgentTeam.Console.Workflows;

/// <summary>
/// Input for the requirement analysis workflow/activity.
/// Maps to run-requirement-analysis environment: PLATFORM, REPO_URL, ISSUE_NUMBER.
/// </summary>
public sealed record RequirementAnalysisInput(
    string PlatformName,
    string RepoUrl,
    int IssueNumber
);
