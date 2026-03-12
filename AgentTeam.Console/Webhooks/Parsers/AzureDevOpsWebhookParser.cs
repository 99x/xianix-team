using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentTeam.Console.Webhooks.Models;
using Microsoft.Extensions.Logging;

namespace AgentTeam.Console.Webhooks.Parsers;

/// <summary>
/// Parses Azure DevOps git.pullrequest webhook payloads.
/// Supports both "All" (full resource) and "Minimal" (url + id only) webhook configurations.
/// When minimal, fetches full PR details from resource.url.
/// </summary>
public sealed class AzureDevOpsWebhookParser : IWebhookPayloadParser
{
    private static readonly HttpClient SharedHttpClient = new();
    private static readonly ILogger Logger = LoggerFactory
        .Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug))
        .CreateLogger<AzureDevOpsWebhookParser>();

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
                   resource.TryGetProperty("pullRequestId", out _) &&
                   (resource.TryGetProperty("repository", out _) || resource.TryGetProperty("url", out _));
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
            var resource = root.GetProperty("resource");

            // Handle "Minimal" webhook payload: resource has url + pullRequestId but no repository
            if (!resource.TryGetProperty("repository", out _))
            {
                var fetched = FetchFullPullRequestResourceAsync(resource).GetAwaiter().GetResult();
                if (fetched.ValueKind != JsonValueKind.Undefined)
                {
                    resource = fetched;
                }
                else
                {
                    // API fetch failed (e.g. no AZURE_TOKEN in env). Fall back to extracting from message.
                    return ParseFromMinimalPayload(root, resource, doc.RootElement.Clone());
                }
            }

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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Logger.LogError(ex, "Failed to parse Azure DevOps webhook payload");
            return null;
        }
    }

    /// <summary>
    /// Parses minimal payload when API fetch fails. Extracts repo URL from message/detailedMessage links.
    /// </summary>
    private static PrWebhookContext? ParseFromMinimalPayload(JsonElement root, JsonElement resource, JsonElement rawPayload)
    {
        var prNumber = resource.TryGetProperty("pullRequestId", out var prProp) ? prProp.GetInt32() : 0;
        if (prNumber <= 0) return null;

        var repoUrl = ExtractRepoUrlFromMessage(root);
        if (string.IsNullOrEmpty(repoUrl)) return null;

        var eventTypeStr = root.TryGetProperty("eventType", out var etProp) ? etProp.GetString() : null;
        var eventType = MapEventTypeToPrWebhookEvent(eventTypeStr, resource);

        return new PrWebhookContext
        {
            Platform = GitProvider.AzureDevOps,
            EventType = eventType,
            RepoUrl = repoUrl,
            PrNumber = prNumber,
            SourceBranch = null,
            TargetBranch = null,
            DiffUrl = null,
            RawPayload = rawPayload,
        };
    }

    private static readonly Regex RepoUrlRegex = new(
        @"https://(?:[^@]+@)?dev\.azure\.com/[^""'<>\s]+/_git/[^""'<>\s]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string? ExtractRepoUrlFromMessage(JsonElement root)
    {
        foreach (var key in new[] { "detailedMessage", "message" })
        {
            if (!root.TryGetProperty(key, out var msg)) continue;
            foreach (var field in new[] { "html", "markdown", "text" })
            {
                if (!msg.TryGetProperty(field, out var content)) continue;
                var text = content.GetString();
                if (string.IsNullOrEmpty(text)) continue;
                var match = RepoUrlRegex.Match(text);
                if (!match.Success) continue;
                var url = match.Value.TrimEnd('/');
                // Exclude pull request links; extract repo base (strip /pullrequest/N)
                var prIdx = url.IndexOf("/pullrequest/", StringComparison.OrdinalIgnoreCase);
                if (prIdx >= 0) url = url[..prIdx];
                url = NormalizeAzureRepoUrl(url);
                if (!string.IsNullOrEmpty(url)) return url;
            }
        }
        return null;
    }

    /// <summary>
    /// Fetches full pull request resource from Azure DevOps API when webhook sends minimal payload.
    /// Uses AZURE_TOKEN or GIT_TOKEN for auth when available (required for private repos).
    /// </summary>
    private static async Task<JsonElement> FetchFullPullRequestResourceAsync(JsonElement minimalResource)
    {
        if (!minimalResource.TryGetProperty("url", out var urlProp))
            return default;
        var url = urlProp.GetString();
        if (string.IsNullOrEmpty(url))
            return default;

        var apiUrl = url.Contains('?', StringComparison.Ordinal)
            ? url + "&api-version=7.0"
            : url + "?api-version=7.0";

        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        var token = Environment.GetEnvironmentVariable("AZURE_TOKEN")
            ?? Environment.GetEnvironmentVariable("GIT_TOKEN");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{token}")));

        try
        {
            var response = await SharedHttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning(
                    "ADO API returned {StatusCode} for {Url}. Minimal payload fallback will be used.",
                    (int)response.StatusCode, apiUrl);
                return default;
            }
            var json = await response.Content.ReadAsStringAsync();
            using var fetched = JsonDocument.Parse(json);
            return fetched.RootElement.Clone();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Logger.LogWarning(ex, "Failed to fetch full PR resource from ADO API at {Url}", apiUrl);
            return default;
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
        {
            var url = remoteUrl.GetString();
            return NormalizeAzureRepoUrl(url);
        }

        // Build from web URL if remoteUrl not present
        if (repository.TryGetProperty("webUrl", out var webUrl))
        {
            var url = webUrl.GetString();
            if (string.IsNullOrEmpty(url)) return null;
            // Convert web URL to clone URL: .../_git/repo -> .../_git/repo (Azure uses same for clone)
            url = url.EndsWith(".git", StringComparison.Ordinal) ? url : $"{url.TrimEnd('/')}.git";
            return NormalizeAzureRepoUrl(url);
        }

        return null;
    }

    /// <summary>
    /// Strips org@ prefix from Azure URLs (e.g. https://OrgName@dev.azure.com/... -> https://dev.azure.com/...).
    /// The prefix causes "Bad hostname" when injecting token for git clone.
    /// </summary>
    private static string? NormalizeAzureRepoUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        return System.Text.RegularExpressions.Regex.Replace(
            url,
            @"^https://[^@]+@dev\.azure\.com",
            "https://dev.azure.com",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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
