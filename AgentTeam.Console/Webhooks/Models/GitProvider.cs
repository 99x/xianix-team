namespace AgentTeam.Console.Webhooks.Models;

/// <summary>
/// Identifies the source control provider for webhook events.
/// </summary>
public enum GitProvider
{
    GitHub,
    AzureDevOps,
}

/// <summary>
/// Maps <see cref="GitProvider"/> to workflow/script environment values (e.g. PLATFORM for run-pr-review).
/// </summary>
public static class GitProviderExtensions
{
    public static string ToWorkflowPlatformName(this GitProvider platform) =>
        platform switch
        {
            GitProvider.GitHub => "github",
            GitProvider.AzureDevOps => "azure-devops",
            _ => platform.ToString().ToLowerInvariant(),
        };
}
