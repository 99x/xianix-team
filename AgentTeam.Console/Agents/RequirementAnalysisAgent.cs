using System.Text.Json;
using AgentTeam.Console.Webhooks;
using AgentTeam.Console.Webhooks.Parsers;
using AgentTeam.Console.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;

namespace AgentTeam.Console.Agents;

/// <summary>
/// Requirement Analysis Agent: registers with Xians platform and handles issue webhook events.
/// </summary>
public sealed class RequirementAnalysisAgent
{
    private readonly dynamic _agent;

    private RequirementAnalysisAgent(dynamic agent) => _agent = agent;

    /// <summary>
    /// Registers the Requirement Analysis Agent with the Xians platform, including workflow and webhook handlers.
    /// </summary>
    public static RequirementAnalysisAgent Register(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new()
        {
            Name = "Requirement Analysis Agent",
            Category = "AIDLC",
            Summary = "Elaborates backlog items into structured requirements with acceptance criteria, dependencies, and gap detection.",
            Description = "This agent analyzes GitHub issues and backlog items to produce fully groomed requirements with acceptance criteria, dependency mapping, risk identification, and gap detection.",
            Version = "1.0.0",
            Author = "99x",
            IsTemplate = false
        });

        agent.Workflows.DefineCustom<RequirementAnalysisWorkflow>(new WorkflowOptions { Activable = false })
            .AddActivity<RunRequirementAnalysisActivity>();

        var webhookResolver = new IssueWebhookParserResolver(
            new GitHubIssueWebhookParser()
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

            var issueContext = webhookResolver.Parse(rawPayload, headers);
            if (issueContext is null)
            {
                System.Console.WriteLine("Unrecognized webhook provider, invalid payload, or non-analyzable issue event");
                System.Console.WriteLine($"Payload length: {rawPayload?.Length ?? 0}, Headers: {(headers?.Count ?? 0)}");
                if (!string.IsNullOrEmpty(rawPayload))
                    System.Console.WriteLine($"Payload preview: {(rawPayload.Length > 300 ? rawPayload[..300] + "..." : rawPayload)}");
                return;
            }

            System.Console.WriteLine($"Webhook: [{issueContext.PlatformName}] repo={issueContext.RepoUrl} issue=#{issueContext.IssueNumber}");

            var input = new RequirementAnalysisInput(
                issueContext.PlatformName,
                issueContext.RepoUrl,
                issueContext.IssueNumber);

            await XiansContext.Workflows.StartAsync<RequirementAnalysisWorkflow>(args: new[] { input }, Guid.NewGuid().ToString());
        });

        return new RequirementAnalysisAgent(agent);
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
