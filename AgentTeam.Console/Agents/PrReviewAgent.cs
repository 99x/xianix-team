using System.Text.Json;
using AgentTeam.Console.Webhooks;
using AgentTeam.Console.Webhooks.Parsers;
using AgentTeam.Console.Workflows;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;

namespace AgentTeam.Console.Agents;

/// <summary>
/// PR Review Agent: handles PR webhook events (parse payload, start workflow).
/// Agent registration and webhook listener are configured in Program.cs.
/// </summary>
public static class PrReviewAgent
{
    private static readonly WebhookParserResolver WebhookResolver = new(
        new GitHubWebhookParser(),
        new AzureDevOpsWebhookParser()
    );

    /// <summary>
    /// Handles a PR webhook: parses payload and starts the PR review workflow.
    /// Invoked from Program.cs when webhook name is "pr-reviewer".
    /// </summary>
    public static async Task HandleWebhookAsync(dynamic context, ILogger logger)
    {
        var tenant = (string?)context.Webhook.TenantId;

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

        var prContext = WebhookResolver.Parse(rawPayload, headers);
        if (prContext is null)
        {
            logger.LogWarning(
                "Unrecognized webhook payload (length={PayloadLength}, headers={HeaderCount}). Preview: {PayloadPreview}",
                rawPayload?.Length ?? 0,
                headers?.Count ?? 0,
                rawPayload?.Length > 0 ? rawPayload[..Math.Min(200, rawPayload.Length)] : "(empty)");
            return;
        }

        logger.LogInformation(
            "Webhook received: [{Platform}] repo={RepoUrl} pr=#{PrNumber}",
            prContext.PlatformName, prContext.RepoUrl, prContext.PrNumber);

        if (string.IsNullOrWhiteSpace(tenant))
        {
            logger.LogWarning("PR webhook missing TenantId; cannot start PR review workflow.");
            return;
        }

        var sourceRef = !string.IsNullOrEmpty(prContext.SourceBranch)
            ? $"refs/heads/{prContext.SourceBranch}"
            : null;

        var input = new PrReviewScriptInput(
            prContext.PlatformName,
            prContext.RepoUrl,
            prContext.PrNumber,
            tenant,
            SourceRef: sourceRef);

        await StartReviewAsync(input, logger);
    }

    /// <summary>
    /// Starts the PR review script workflow from a <see cref="PrReviewScriptInput"/> (no webhook payload).
    /// </summary>
    public static async Task StartReviewAsync(PrReviewScriptInput input, ILogger? logger = null)
    {
        var workflowId = BuildWorkflowId(input);
        logger?.LogDebug("Starting workflow {WorkflowId}", workflowId);
        await XiansContext.Workflows.StartAsync<PrReviewScriptWorkflow>(args: new[] { input }, workflowId);
    }

    /// <summary>
    /// Deterministic workflow ID prevents duplicate reviews if the same webhook is delivered more than once.
    /// Include tenant for isolation when multiple tenants share the same process.
    /// </summary>
    private static string BuildWorkflowId(PrReviewScriptInput input) =>
        $"pr-review-{SanitizeForId(input.TenantId)}-{input.PlatformName}-{SanitizeForId(input.RepoUrl)}-{input.PrNumber}";

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
                inner.TryGetProperty("pull_request", out _) && inner.TryGetProperty("repository", out _))
                return inner.GetRawText();
        }
        catch (JsonException)
        {
            // Not valid JSON or no payload wrapper — return trimmed form-decoded value as-is
        }

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
        catch (Exception)
        {
            return null;
        }
    }

    private static string SanitizeForId(string value)
    {
        // Keep only alphanumeric, hyphens, dots, underscores — safe for Temporal workflow IDs
        var span = value.AsSpan();
        var chars = new char[span.Length];
        var len = 0;
        foreach (var c in span)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '_')
                chars[len++] = c;
            else if (c == '/' || c == ':')
                chars[len++] = '-';
        }
        var sanitized = new string(chars, 0, len).Trim('-');
        // Truncate so total workflow ID stays within Temporal's 1000-char limit
        return sanitized.Length > 200 ? sanitized[^200..] : sanitized;
    }
}
