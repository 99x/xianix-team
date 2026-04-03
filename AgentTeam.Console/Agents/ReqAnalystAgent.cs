using System.Text.Json;
using AgentTeam.Console.Rules;
using AgentTeam.Console.Workflows;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Xians.Lib.Agents.Core;

namespace AgentTeam.Console.Agents;

/// <summary>
/// Requirement Analysis Agent: handles issue webhook events (extract params via LLM, start workflow).
/// Agent registration and webhook listener are configured in Program.cs.
/// </summary>
public static class ReqAnalystAgent
{
    private static readonly ChatResponseFormat ExtractionSchema = ChatResponseFormat.CreateJsonSchemaFormat(
        "issue_webhook_extraction",
        BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                platformName = new
                {
                    type = "string",
                    description = "Source control platform, e.g. 'github', 'azure-devops', 'gitlab'."
                },
                repoUrl = new
                {
                    type = "string",
                    description = "Full HTTPS URL of the repository (e.g. 'https://github.com/owner/repo')."
                },
                issueNumber = new
                {
                    type = "integer",
                    description = "Issue or work-item number."
                }
            },
            required = new[] { "platformName", "repoUrl", "issueNumber" },
            additionalProperties = false
        }),
        jsonSchemaIsStrict: true);

    private const string ExtractionPrompt =
        """
        You are a webhook payload parser. Given a raw JSON webhook body from any git platform
        (GitHub, Azure DevOps, GitLab, Bitbucket, etc.), extract exactly three fields:
        - platformName: lowercase platform identifier (e.g. "github", "azure-devops", "gitlab").
        - repoUrl: the full HTTPS clone/browse URL of the repository.
        - issueNumber: the issue or work-item number as an integer.
        Respond ONLY with the JSON object matching the provided schema.
        """;

    /// <summary>
    /// Handles an issue webhook: reads tenant and payload from context, LLM-extracts parameters,
    /// then starts the requirement analysis workflow.
    /// Invoked from Program.cs when webhook name starts with "req-analyst".
    /// </summary>
    public static async Task HandleWebhookAsync(object webhookContext, ILogger logger, string openAiApiKey)
    {
        dynamic context = webhookContext;
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
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            logger.LogWarning("Issue webhook payload is empty after normalization; nothing to analyse.");
            return;
        }

        if (string.IsNullOrWhiteSpace(tenant))
        {
            logger.LogWarning("Issue webhook missing TenantId; cannot start requirement analysis workflow.");
            return;
        }

        if (!await WebhookRuleEvaluator.ShouldProcessAsync("req-analyst", rawPayload, logger))
        {
            logger.LogInformation("Webhook did not match any filter rule. Skipping analysis.");
            return;
        }

        logger.LogInformation("Extracting issue context from webhook payload via LLM ({Length} chars)…", rawPayload.Length);

        var chatClient = new OpenAIClient(openAiApiKey).GetChatClient("gpt-4o-mini");
        var completion = await chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(ExtractionPrompt),
                new UserChatMessage(rawPayload)
            ],
            new ChatCompletionOptions { ResponseFormat = ExtractionSchema });

        var json = completion.Value.Content[0].Text;
        using var doc = JsonDocument.Parse(json);
        var extracted = doc.RootElement;

        var platformName = extracted.GetProperty("platformName").GetString()!;
        var repoUrl = extracted.GetProperty("repoUrl").GetString()!;
        var issueNumber = extracted.GetProperty("issueNumber").GetInt32();

        logger.LogInformation(
            "Webhook received: [{Platform}] repo={RepoUrl} issue=#{IssueNumber}",
            platformName, repoUrl, issueNumber);

        var input = new RequirementAnalysisInput(platformName, repoUrl, issueNumber, tenant);
        await StartAnalysisAsync(input, logger);
    }

    /// <summary>
    /// Starts the requirement analysis workflow with explicit parameters (no webhook payload).
    /// Use when invoking from code, CLI, or tests instead of <see cref="HandleWebhookAsync"/>.
    /// </summary>
    public static Task StartAnalysisAsync(
        string platformName,
        string repoUrl,
        int issueNumber,
        string tenantId,
        ILogger? logger = null)
    {
        var input = new RequirementAnalysisInput(platformName, repoUrl, issueNumber, tenantId);
        return StartAnalysisAsync(input, logger);
    }

    /// <summary>
    /// Starts the requirement analysis workflow from a <see cref="RequirementAnalysisInput"/> (no webhook payload).
    /// </summary>
    public static async Task StartAnalysisAsync(RequirementAnalysisInput input, ILogger? logger = null)
    {
        var workflowId = BuildWorkflowId(input);
        logger?.LogDebug("Starting workflow {WorkflowId}", workflowId);
        await XiansContext.Workflows.StartAsync<RequirementAnalysisWorkflow>(args: new[] { input }, workflowId);
    }

    private static string BuildWorkflowId(RequirementAnalysisInput input) =>
        $"req-analysis-{SanitizeForId(input.TenantId)}-{input.PlatformName}-{SanitizeForId(input.RepoUrl)}-{input.IssueNumber}";

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
        catch (JsonException)
        {
            // keep trimmed
        }

        return trimmed;
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
        return sanitized.Length > 200 ? sanitized[^200..] : sanitized;
    }
}
