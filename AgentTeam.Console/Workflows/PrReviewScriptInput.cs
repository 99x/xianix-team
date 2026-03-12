namespace AgentTeam.Console.Workflows;

/// <summary>
/// Input for the PR review script workflow/activity.
/// Maps to run-pr-review environment: PLATFORM, REPO_URL, PR_NUMBER.
/// </summary>
public sealed record PrReviewScriptInput(
    string PlatformName,
    string RepoUrl,
    int PrNumber
);
