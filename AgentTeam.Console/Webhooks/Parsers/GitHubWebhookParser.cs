using System.Text.Json;
using AgentTeam.Console.Webhooks.Models;
using Microsoft.Extensions.Logging;

namespace AgentTeam.Console.Webhooks.Parsers;

/// <summary>
/// Parses GitHub pull_request webhook payloads.
/// </summary>
public sealed class GitHubWebhookParser : IWebhookPayloadParser
{
    private static readonly ILogger Logger = LoggerFactory
        .Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug))
        .CreateLogger<GitHubWebhookParser>();

    private const string GitHubEventHeader = "X-GitHub-Event";
    private const string PullRequestEvent = "pull_request";

    public bool CanParse(string rawPayload, IReadOnlyDictionary<string, string>? headers = null)
    {
        if (headers is not null && headers.TryGetValue(GitHubEventHeader, out var eventType))
            return string.Equals(eventType, PullRequestEvent, StringComparison.OrdinalIgnoreCase);

        // Fallback: payload structure — GitHub has top-level "pull_request"/"pullRequest" and "repository"
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;
            var hasPr = root.TryGetProperty("pull_request", out _) || root.TryGetProperty("pullRequest", out _);
            var hasRepo = root.TryGetProperty("repository", out _);
            return hasPr && hasRepo;
        }
        catch (JsonException)
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

            var pullRequest = GetProperty(root, "pull_request", "pullRequest");
            var repository = GetProperty(root, "repository");

            var action = root.TryGetProperty("action", out var actionProp)
                ? actionProp.GetString()
                : null;

            if (!IsReviewableAction(action))
                return null;

            var eventType = MapActionToEvent(action);
            var repoUrl = GetRepoUrl(repository);
            var prNumber = pullRequest.GetProperty("number").GetInt32();
            var head = GetProperty(pullRequest, "head");
            var sourceBranch = head.ValueKind != JsonValueKind.Undefined && head.TryGetProperty("ref", out var headRef)
                ? headRef.GetString()
                : null;
            var @base = GetProperty(pullRequest, "base");
            var targetBranch = @base.ValueKind != JsonValueKind.Undefined && @base.TryGetProperty("ref", out var baseRef)
                ? baseRef.GetString()
                : null;
            var diffProp = GetProperty(pullRequest, "diff_url", "diffUrl");
            var diffUrl = diffProp.ValueKind != JsonValueKind.Undefined ? diffProp.GetString() : null;

            return new PrWebhookContext
            {
                Platform = GitProvider.GitHub,
                EventType = eventType,
                RepoUrl = repoUrl ?? throw new InvalidOperationException("GitHub payload missing repository clone URL"),
                PrNumber = prNumber,
                SourceBranch = sourceBranch,
                TargetBranch = targetBranch,
                DiffUrl = diffUrl,
                RawPayload = doc.RootElement.Clone(),
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Logger.LogError(ex, "Failed to parse GitHub webhook payload");
            return null;
        }
    }

    private static bool IsReviewableAction(string? action) =>
        action?.ToLowerInvariant() switch
        {
            "opened" => true,
            "synchronize" or "synchronized" => true,
            _ => false,
        };

    private static PrWebhookEvent MapActionToEvent(string? action) =>
        action?.ToLowerInvariant() switch
        {
            "opened" => PrWebhookEvent.PullRequestCreated,
            "synchronize" or "synchronized" => PrWebhookEvent.PullRequestSynchronized,
            _ => PrWebhookEvent.PullRequestSynchronized,
        };

    private static JsonElement GetProperty(JsonElement el, params string[] names)
    {
        foreach (var name in names)
            if (el.TryGetProperty(name, out var prop)) return prop;
        return default;
    }

    private static string? GetRepoUrl(JsonElement repository)
    {
        var cloneUrl = GetProperty(repository, "clone_url", "cloneUrl");
        if (cloneUrl.ValueKind != JsonValueKind.Undefined)
            return cloneUrl.GetString();
        var htmlUrl = GetProperty(repository, "html_url", "htmlUrl");
        if (htmlUrl.ValueKind != JsonValueKind.Undefined)
        {
            var url = htmlUrl.GetString();
            return url?.EndsWith(".git", StringComparison.Ordinal) == true ? url : $"{url}.git";
        }
        return null;
    }
}
