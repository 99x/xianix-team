using System.Text.Json;
using AgentTeam.Console.Webhooks;
using AgentTeam.Console.Webhooks.Parsers;
using AgentTeam.Console.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;

namespace AgentTeam.Console.Agents;

/// <summary>
/// PR Review Agent: registers with Xians platform and handles PR webhook events.
/// </summary>
public sealed class PrReviewAgent
{
    private readonly dynamic _agent;

    private PrReviewAgent(dynamic agent) => _agent = agent;

    /// <summary>
    /// Registers the PR Review Agent with the Xians platform, including workflow and webhook handlers.
    /// </summary>
    public static PrReviewAgent Register(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new()
        {
            Name = "PR Review Agent",
            Category = "AIDLC",
            Summary = "Reviews pull requests for code quality, security, and compliance.",
            Description = "This agent reviews pull requests for code quality, security, and compliance.",
            Version = "1.0.0",
            Author = "99x",
            IsTemplate = false
        });

        agent.Workflows.DefineCustom<PrReviewScriptWorkflow>( new WorkflowOptions { Activable = false } )
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
                System.Console.WriteLine("Unrecognized webhook provider or invalid payload");
                System.Console.WriteLine($"Payload length: {rawPayload?.Length ?? 0}, Headers: {(headers?.Count ?? 0)}");
                if (!string.IsNullOrEmpty(rawPayload))
                    System.Console.WriteLine($"Payload preview: {(rawPayload.Length > 300 ? rawPayload[..300] + "..." : rawPayload)}");
                return;
            }

            System.Console.WriteLine($"Webhook: [{prContext.PlatformName}] repo={prContext.RepoUrl} pr=#{prContext.PrNumber}");

            var input = new PrReviewScriptInput(
                prContext.PlatformName,
                prContext.RepoUrl,
                prContext.PrNumber);

            await XiansContext.Workflows.StartAsync<PrReviewScriptWorkflow>(args: new[] { input }, Guid.NewGuid().ToString());
        });

        return new PrReviewAgent(agent);
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
