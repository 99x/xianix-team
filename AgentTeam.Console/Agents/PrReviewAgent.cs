using System.Text.Json;
using AgentTeam.Console.Webhooks;
using AgentTeam.Console.Webhooks.Parsers;
using AgentTeam.Console.Workflows;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;

namespace AgentTeam.Console.Agents;

/// <summary>
/// PR Review Agent: registers with Xians platform and handles PR webhook events.
/// </summary>
public sealed class PrReviewAgent
{
    private readonly dynamic _agent;
    private readonly ILogger<PrReviewAgent> _logger;

    private PrReviewAgent(dynamic agent, ILogger<PrReviewAgent> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    /// <summary>
    /// Registers the PR Review Agent with the Xians platform, including workflow and webhook handlers.
    /// </summary>
    public static PrReviewAgent Register(XiansPlatform platform)
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var logger = loggerFactory.CreateLogger<PrReviewAgent>();

        var agent = platform.Agents.Register(new()
        {
            Name = "PR Review Agent",
            Category = "AIDLC",
            Summary = "Reviews pull requests for code quality, security, and compliance.",
            Description = "This agent reviews pull requests for code quality, security, and compliance.",
            Version = "1.0.0",
            Author = "99x",
            IsTemplate = true
        });

        agent.Workflows.DefineCustom<PrReviewScriptWorkflow>(new WorkflowOptions { Activable = false })
            .AddActivity<RunPrReviewScriptActivity>();

        var webhookResolver = new WebhookParserResolver(
            new GitHubWebhookParser(),
            new AzureDevOpsWebhookParser()
        );

        var integratorWorkflow = agent.Workflows.DefineIntegrator();
        integratorWorkflow.OnWebhook(async (context) =>
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

            var prContext = webhookResolver.Parse(rawPayload, headers);
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

            var sourceRef = !string.IsNullOrEmpty(prContext.SourceBranch)
                ? $"refs/heads/{prContext.SourceBranch}"
                : null;

            var input = new PrReviewScriptInput(
                prContext.PlatformName,
                prContext.RepoUrl,
                prContext.PrNumber,
                SourceRef: sourceRef);

            // Deterministic workflow ID prevents duplicate reviews if the same webhook is delivered more than once.
            var workflowId = $"pr-review-{prContext.PlatformName}-{SanitizeForId(prContext.RepoUrl)}-{prContext.PrNumber}";

            logger.LogDebug("Starting workflow {WorkflowId}", workflowId);
            await XiansContext.Workflows.StartAsync<PrReviewScriptWorkflow>(args: new[] { input }, workflowId);
        });

        return new PrReviewAgent(agent, logger);
    }

    /// <summary>
    /// Runs the agent (starts webhook listener and workflows).
    /// </summary>
    public Task RunAllAsync() => _agent.RunAllAsync();

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
