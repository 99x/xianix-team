using System.Text.Json;
using AgentTeam.Console.Webhooks.Models;

namespace AgentTeam.Console.Webhooks.Parsers;

/// <summary>
/// Parses Azure DevOps git.pullrequest webhook payloads.
/// </summary>
public sealed class AzureDevOpsWebhookParser : IWebhookPayloadParser
{
    private static readonly string[] KnownEventTypes =
    [
        "git.pullrequest.created",
        "git.pullrequest.updated",
        "ms.vss-code.git-pullrequest-event",
    ];

    public bool CanParse(string rawPayload, IReadOnlyDictionary<string, string>? headers = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            if (root.TryGetProperty("eventType", out var eventType))
            {
                var value = eventType.GetString();
                return !string.IsNullOrEmpty(value) &&
                       KnownEventTypes.Any(e => value.StartsWith("git.pullrequest.", StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(value, "ms.vss-code.git-pullrequest-event", StringComparison.OrdinalIgnoreCase));
            }

            return root.TryGetProperty("resource", out var resource) &&
                   resource.TryGetProperty("repository", out _) &&
                   resource.TryGetProperty("pullRequestId", out _);
        }
        catch
        {
            return false;
        }
    }

    public PrWebhookContext? Parse(string rawPayload, IReadOnlyDictionary<string, string>? headers = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;
            var resource = root.GetProperty("resource");

            var eventTypeStr = root.TryGetProperty("eventType", out var eventTypeProp)
                ? eventTypeProp.GetString()
                : null;
            var eventType = MapEventTypeToPrWebhookEvent(eventTypeStr, resource);

            var repository = resource.GetProperty("repository");
            var repoUrl = GetRepoUrl(repository);
            var prNumber = resource.GetProperty("pullRequestId").GetInt32();
            var sourceBranch = StripRefPrefix(resource.TryGetProperty("sourceRefName", out var src) ? src.GetString() : null);
            var targetBranch = StripRefPrefix(resource.TryGetProperty("targetRefName", out var tgt) ? tgt.GetString() : null);

            return new PrWebhookContext
            {
                Platform = GitProvider.AzureDevOps,
                EventType = eventType,
                RepoUrl = repoUrl ?? throw new InvalidOperationException("Azure DevOps payload missing repository remote URL"),
                PrNumber = prNumber,
                SourceBranch = sourceBranch,
                TargetBranch = targetBranch,
                DiffUrl = null, // Azure DevOps typically requires REST API for diff
                RawPayload = doc.RootElement.Clone(),
            };
        }
        catch
        {
            return null;
        }
    }

    private static PrWebhookEvent MapEventTypeToPrWebhookEvent(string? eventType, JsonElement resource)
    {
        if (string.Equals(eventType, "git.pullrequest.created", StringComparison.OrdinalIgnoreCase))
            return PrWebhookEvent.PullRequestCreated;

        if (string.Equals(eventType, "git.pullrequest.updated", StringComparison.OrdinalIgnoreCase))
        {
            // Updated can mean synchronized (new commits) or status change
            // Treat as synchronized for PR review purposes
            return PrWebhookEvent.PullRequestSynchronized;
        }

        if (string.Equals(eventType, "ms.vss-code.git-pullrequest-event", StringComparison.OrdinalIgnoreCase))
        {
            var status = resource.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
            return string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)
                ? PrWebhookEvent.PullRequestSynchronized
                : PrWebhookEvent.PullRequestCreated;
        }

        return PrWebhookEvent.PullRequestSynchronized;
    }

    private static string? GetRepoUrl(JsonElement repository)
    {
        if (repository.TryGetProperty("remoteUrl", out var remoteUrl))
            return remoteUrl.GetString();

        // Build from web URL if remoteUrl not present
        if (repository.TryGetProperty("webUrl", out var webUrl))
        {
            var url = webUrl.GetString();
            if (string.IsNullOrEmpty(url)) return null;
            // Convert web URL to clone URL: .../_git/repo -> .../_git/repo (Azure uses same for clone)
            return url.EndsWith(".git", StringComparison.Ordinal) ? url : $"{url.TrimEnd('/')}.git";
        }

        return null;
    }

    private static string? StripRefPrefix(string? refName)
    {
        if (string.IsNullOrEmpty(refName)) return null;
        const string prefix = "refs/heads/";
        return refName.StartsWith(prefix, StringComparison.Ordinal)
            ? refName[prefix.Length..]
            : refName;
    }
}
