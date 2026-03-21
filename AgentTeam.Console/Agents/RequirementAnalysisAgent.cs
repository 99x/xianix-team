using System.Text.Json;
using AgentTeam.Console.Webhooks;
using AgentTeam.Console.Webhooks.Parsers;
using AgentTeam.Console.Workflows;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;

namespace AgentTeam.Console.Agents;

/// <summary>
/// Requirement Analysis Agent: handles issue webhook events (parse payload, start workflow).
/// Agent registration and webhook listener are configured in Program.cs.
/// </summary>
public static class RequirementAnalysisAgent
{
    private static readonly IssueWebhookParserResolver WebhookResolver = new(
        new GitHubIssueWebhookParser()
    );

    /// <summary>
    /// Handles an issue webhook: parses payload and starts the requirement analysis workflow.
    /// Invoked from Program.cs when webhook name is "req-analyst".
    /// </summary>
    public static async Task HandleWebhookAsync(dynamic context, ILogger logger)
    {
        var payload = (object?)context.Webhook.Payload;
        var rawPayload = payload switch
        {
            string s => s,
            JsonElement je => je.GetRawText(),
            not null => JsonSerializer.Serialize(payload),
            _ => ""
        };

        rawPayload = NormalizePayload(rawPayload);
        var headers = GetWebhookHeaders((object)context);

        var issueContext = WebhookResolver.Parse(rawPayload, headers);
        if (issueContext is null)
        {
            logger.LogWarning(
                "Unrecognized webhook provider, invalid payload, or non-analyzable issue event (length={PayloadLength}, headers={HeaderCount}). Preview: {PayloadPreview}",
                rawPayload?.Length ?? 0,
                headers?.Count ?? 0,
                !string.IsNullOrEmpty(rawPayload) ? rawPayload[..Math.Min(300, rawPayload.Length)] : "(empty)");
            return;
        }

        logger.LogInformation(
            "Webhook received: [{Platform}] repo={RepoUrl} issue=#{IssueNumber}",
            issueContext.PlatformName, issueContext.RepoUrl, issueContext.IssueNumber);

        var input = new RequirementAnalysisInput(
            issueContext.PlatformName,
            issueContext.RepoUrl,
            issueContext.IssueNumber);

        await XiansContext.Workflows.StartAsync<RequirementAnalysisWorkflow>(args: new[] { input }, Guid.NewGuid().ToString());
    }

    private static string NormalizePayload(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var trimmed = raw.Trim();

        if (!trimmed.StartsWith('{'))
        {
            if (trimmed.StartsWith("payload=", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = Uri.UnescapeDataString(trimmed["payload=".Length..].Replace('+', ' ')).Trim();
            }
            else
            {
                foreach (var pair in trimmed.Split('&'))
                {
                    var eq = pair.IndexOf('=');
                    if (eq > 0 && string.Equals(pair[..eq], "payload", StringComparison.OrdinalIgnoreCase))
                    {
                        trimmed = Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' ')).Trim();
                        break;
                    }
                }
            }
        }

        if (!trimmed.StartsWith('{')) return raw;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            if (root.TryGetProperty("payload", out var inner) && inner.ValueKind == JsonValueKind.Object &&
                inner.TryGetProperty("issue", out _) && inner.TryGetProperty("repository", out _))
                return inner.GetRawText();
        }
        catch { /* not JSON or no payload wrapper */ }

        return trimmed;
    }

    private static IReadOnlyDictionary<string, string>? GetWebhookHeaders(object context)
    {
        try
        {
            var webhook = context?.GetType().GetProperty("Webhook")?.GetValue(context);
            if (webhook is null) return null;
            var headers = webhook.GetType().GetProperty("Headers")?.GetValue(webhook);
            if (headers is null) return null;
            if (headers is IReadOnlyDictionary<string, string> dict) return dict;
            if (headers is IEnumerable<KeyValuePair<string, string>> strEnum)
                return strEnum.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            if (headers is IEnumerable<KeyValuePair<string, object>> objEnum)
                return objEnum.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "", StringComparer.OrdinalIgnoreCase);
            return null;
        }
        catch { return null; }
    }
}
