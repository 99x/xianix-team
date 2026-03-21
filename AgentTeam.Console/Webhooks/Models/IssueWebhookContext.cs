using System.Text.Json;

namespace AgentTeam.Console.Webhooks.Models;

/// <summary>
/// Unified issue context extracted from provider-specific webhook payloads.
/// Maps to run-requirement-analysis environment: PLATFORM, REPO_URL, ISSUE_NUMBER.
/// </summary>
public sealed class IssueWebhookContext
{
    public required GitProvider Platform { get; init; }
    public required IssueWebhookEvent EventType { get; init; }
    public required string RepoUrl { get; init; }
    public required int IssueNumber { get; init; }
    public string? LabelAdded { get; init; }
    public JsonElement? RawPayload { get; init; }

    /// <summary>
    /// Returns the platform name as used in run-requirement-analysis (e.g., "github", "azure-devops").
    /// </summary>
    public string PlatformName =>
        Platform switch
        {
            GitProvider.GitHub => "github",
            GitProvider.AzureDevOps => "azure-devops",
            _ => Platform.ToString().ToLowerInvariant(),
        };
}
