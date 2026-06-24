using System.Text.Json;
using AgentTeam.Console.Webhooks;
using AgentTeam.Console.Webhooks.Parsers;
using AgentTeam.Console.Workflows;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;

namespace AgentTeam.Console.Agents;

/// <summary>
/// Impact Analysis Agent: registers with Xians platform and handles PR webhook events
/// to produce QA-focused impact reports highlighting high-risk areas.
/// </summary>
public sealed class ImpactAnalysisAgent
{
    private readonly dynamic _agent;
    private readonly ILogger<ImpactAnalysisAgent> _logger;

    private ImpactAnalysisAgent(dynamic agent, ILogger<ImpactAnalysisAgent> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    /// <summary>
    /// Registers the Impact Analysis Agent with the Xians platform, including workflow and webhook handlers.
    /// </summary>
    public static ImpactAnalysisAgent Register(XiansPlatform platform)
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var logger = loggerFactory.CreateLogger<ImpactAnalysisAgent>();

        var agent = platform.Agents.Register(new()
        {
            Name = "Impact Analysis Agent",
            Category = "AIDLC",
            Summary = "Analyzes PR changes for functional impact and informs QA about high-risk areas requiring focused testing.",
            Description = "This agent analyzes pull request changes to identify impacted functionality, trace blast radius through dependencies, map code changes to user-facing features, and produce a prioritized QA-focused impact report with regression risk assessment.",
            Version = "1.0.0",
            Author = "99x",
            IsTemplate = true
        });

        agent.Workflows.DefineCustom<ImpactAnalysisWorkflow>(new WorkflowOptions { Activable = false })
            .AddActivity<RunImpactAnalysisActivity>();

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

            var input = new ImpactAnalysisInput(
                prContext.PlatformName,
                prContext.RepoUrl,
                prContext.PrNumber,
                SourceRef: sourceRef);

            // Deterministic workflow ID prevents duplicate analyses if the same webhook is delivered more than once.
            var workflowId = $"impact-analysis-{prContext.PlatformName}-{SanitizeForId(prContext.RepoUrl)}-{prContext.PrNumber}";

            logger.LogDebug("Starting workflow {WorkflowId}", workflowId);
            await XiansContext.Workflows.StartAsync<ImpactAnalysisWorkflow>(args: new[] { input }, workflowId);
        });

        return new ImpactAnalysisAgent(agent, logger);
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
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }
}
