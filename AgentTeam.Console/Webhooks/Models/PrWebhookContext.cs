using System.Text.Json;

namespace AgentTeam.Console.Webhooks.Models;

/// <summary>
/// Unified PR context extracted from provider-specific webhook payloads.
/// Maps to run-pr-review environment: PLATFORM, REPO_URL, PR_NUMBER.
/// </summary>
public sealed class PrWebhookContext
{
    public required GitProvider Platform { get; init; }
    public required PrWebhookEvent EventType { get; init; }
    public required string RepoUrl { get; init; }
    public required int PrNumber { get; init; }
    public string? SourceBranch { get; init; }
    public string? TargetBranch { get; init; }
    public string? DiffUrl { get; init; }
    public JsonElement? RawPayload { get; init; }

    /// <summary>
    /// Returns the platform name as used in run-pr-review (e.g., "github", "azure-devops").
    /// </summary>
    public string PlatformName => Platform.ToWorkflowPlatformName();
}
