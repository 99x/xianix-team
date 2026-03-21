using System.Text.Json;
using AgentTeam.Console.Webhooks.Models;

namespace AgentTeam.Console.Webhooks.Parsers;

/// <summary>
/// Parses GitHub issues webhook payloads.
/// </summary>
public sealed class GitHubIssueWebhookParser : IIssueWebhookPayloadParser
{
    private const string GitHubEventHeader = "X-GitHub-Event";
    private const string IssuesEvent = "issues";

    /// <summary>
    /// Labels that trigger requirement analysis when added to an issue.
    /// </summary>
    private static readonly HashSet<string> TriggerLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "needs-elaboration",
        "needs-analysis",
        "analyze",
    };

    public bool CanParse(string rawPayload, IReadOnlyDictionary<string, string>? headers = null)
    {
        if (headers is not null && headers.TryGetValue(GitHubEventHeader, out var eventType))
            return string.Equals(eventType, IssuesEvent, StringComparison.OrdinalIgnoreCase);

        // Fallback: payload structure — GitHub issues have top-level "issue" and "repository"
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;
            var hasIssue = root.TryGetProperty("issue", out _);
            var hasRepo = root.TryGetProperty("repository", out _);
            // Ensure it's not a pull_request event (PRs also have "issue" in some payloads)
            var hasPr = root.TryGetProperty("pull_request", out _);
            return hasIssue && hasRepo && !hasPr;
        }
        catch
        {
            return false;
        }
    }

    public IssueWebhookContext? Parse(string rawPayload, IReadOnlyDictionary<string, string>? headers = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            var action = root.TryGetProperty("action", out var actionProp)
                ? actionProp.GetString()
                : null;

            if (!IsAnalyzableAction(action, root))
                return null;

            var issue = root.GetProperty("issue");
            var repository = root.GetProperty("repository");

            var eventType = MapActionToEvent(action);
            var repoUrl = GetRepoUrl(repository);
            var issueNumber = issue.GetProperty("number").GetInt32();

            string? labelAdded = null;
            if (string.Equals(action, "labeled", StringComparison.OrdinalIgnoreCase) &&
                root.TryGetProperty("label", out var labelEl) &&
                labelEl.TryGetProperty("name", out var labelName))
            {
                labelAdded = labelName.GetString();
            }

            return new IssueWebhookContext
            {
                Platform = GitProvider.GitHub,
                EventType = eventType,
                RepoUrl = repoUrl ?? throw new InvalidOperationException("GitHub payload missing repository clone URL"),
                IssueNumber = issueNumber,
                LabelAdded = labelAdded,
                RawPayload = doc.RootElement.Clone(),
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool IsAnalyzableAction(string? action, JsonElement root)
    {
        return action?.ToLowerInvariant() switch
        {
            "opened" => true,
            "edited" => true,
            "labeled" => IsTriggerLabel(root),
            _ => false,
        };
    }

    private static bool IsTriggerLabel(JsonElement root)
    {
        if (root.TryGetProperty("label", out var label) &&
            label.TryGetProperty("name", out var name))
        {
            var labelName = name.GetString();
            return labelName is not null && TriggerLabels.Contains(labelName);
        }
        return false;
    }

    private static IssueWebhookEvent MapActionToEvent(string? action) =>
        action?.ToLowerInvariant() switch
        {
            "opened" => IssueWebhookEvent.IssueOpened,
            "edited" => IssueWebhookEvent.IssueEdited,
            "labeled" => IssueWebhookEvent.IssueLabeled,
            _ => IssueWebhookEvent.IssueOpened,
        };

    private static string? GetRepoUrl(JsonElement repository)
    {
        if (repository.TryGetProperty("clone_url", out var cloneUrl) ||
            repository.TryGetProperty("cloneUrl", out cloneUrl))
        {
            if (cloneUrl.ValueKind != JsonValueKind.Undefined)
                return cloneUrl.GetString();
        }

        if (repository.TryGetProperty("html_url", out var htmlUrl) ||
            repository.TryGetProperty("htmlUrl", out htmlUrl))
        {
            if (htmlUrl.ValueKind != JsonValueKind.Undefined)
            {
                var url = htmlUrl.GetString();
                return url?.EndsWith(".git", StringComparison.Ordinal) == true ? url : $"{url}.git";
            }
        }

        return null;
    }
}
