using System.Text.Json;
using AgentTeam.Console.Rules;
using AgentTeam.Console.Workflows;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Xians.Lib.Agents.Core;

namespace AgentTeam.Console.Agents;

/// <summary>
/// PR Review Agent: handles PR webhook events (LLM extraction from payload, start workflow).
/// Agent registration and webhook listener are configured in Program.cs.
/// </summary>
public static class PrReviewerAgent
{
    /// <summary>Must match <c>AgentName</c> for this agent in <c>agents.json</c>.</summary>
    public const string WebhookFilterAgentName = "pr-reviewer";

    private static readonly ChatResponseFormat PrExtractionSchema = ChatResponseFormat.CreateJsonSchemaFormat(
        "pr_webhook_extraction",
        BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                platformName = new
                {
                    type = "string",
                    description = "Source control platform for run-pr-review, e.g. 'github', 'azure-devops'."
                },
                repoUrl = new
                {
                    type = "string",
                    description = "Full HTTPS URL of the repository (clone or html URL)."
                },
                prNumber = new
                {
                    type = "integer",
                    description = "Pull request number."
                },
                sourceBranch = new
                {
                    type = new[] { "string", "null" },
                    description = "Head/source branch name without refs/heads/ prefix, or null."
                }
            },
            required = new[] { "platformName", "repoUrl", "prNumber", "sourceBranch" },
            additionalProperties = false
        }),
        jsonSchemaIsStrict: true);

    private const string PrExtractionPrompt =
        """
        You are a webhook payload parser. Given a raw JSON webhook body from a git platform
        (GitHub, Azure DevOps, GitLab, Bitbucket, etc.), extract pull request review parameters:
        - platformName: lowercase workflow id (e.g. "github", "azure-devops").
        - repoUrl: full HTTPS repository URL.
        - prNumber: the pull request id as an integer.
        - sourceBranch: the PR head/source branch ref name only (e.g. "feature/foo"), or null if unknown.
        If the payload is not about a single pull request, still return best-effort values from any PR fields present.
        Respond ONLY with the JSON object matching the provided schema.
        """;

    /// <summary>
    /// Handles a PR webhook: extracts repo/pr from the payload via LLM, then starts the PR review workflow.
    /// Invoked from Program.cs when webhook name is "pr-reviewer".
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
            logger.LogWarning("PR webhook payload is empty after normalization; skipping.");
            return;
        }

        if (!await WebhookRuleEvaluator.ShouldProcessAsync(WebhookFilterAgentName, rawPayload, logger))
        {
            logger.LogInformation("Webhook did not match any pr-reviewer filter rule. Skipping.");
            return;
        }

        if (string.IsNullOrWhiteSpace(tenant))
        {
            logger.LogWarning("PR webhook missing TenantId; cannot start PR review workflow.");
            return;
        }

        logger.LogInformation("Extracting PR context from webhook payload via LLM ({Length} chars)…", rawPayload.Length);
        var fromLlm = await TryExtractPrParamsWithLlmAsync(rawPayload, openAiApiKey, logger).ConfigureAwait(false);
        if (fromLlm is null)
        {
            logger.LogWarning(
                "LLM PR extraction failed (length={PayloadLength}). Preview: {PayloadPreview}",
                rawPayload.Length,
                rawPayload.Length > 0 ? rawPayload[..Math.Min(200, rawPayload.Length)] : "(empty)");
            return;
        }

        var input = fromLlm with { TenantId = tenant };
        await StartReviewAsync(input, logger);
    }

    private static async Task<PrReviewScriptInput?> TryExtractPrParamsWithLlmAsync(
        string rawPayloadJson,
        string openAiApiKey,
        ILogger logger)
    {
        try
        {
            var chatClient = new OpenAIClient(openAiApiKey).GetChatClient("gpt-4o-mini");
            var completion = await chatClient.CompleteChatAsync(
                [
                    new SystemChatMessage(PrExtractionPrompt),
                    new UserChatMessage(rawPayloadJson)
                ],
                new ChatCompletionOptions { ResponseFormat = PrExtractionSchema });

            var json = completion.Value.Content[0].Text;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var platformName = root.GetProperty("platformName").GetString();
            var repoUrl = root.GetProperty("repoUrl").GetString();
            var prNumber = root.GetProperty("prNumber").GetInt32();
            string? sourceBranch = null;
            if (root.TryGetProperty("sourceBranch", out var sb) && sb.ValueKind == JsonValueKind.String)
                sourceBranch = sb.GetString();

            if (string.IsNullOrWhiteSpace(platformName) || string.IsNullOrWhiteSpace(repoUrl) || prNumber <= 0)
            {
                logger.LogWarning("LLM PR extraction returned invalid platform, repo, or pr number.");
                return null;
            }

            logger.LogInformation(
                "LLM extraction: [{Platform}] repo={RepoUrl} pr=#{PrNumber}",
                platformName, repoUrl, prNumber);

            var sourceRef = !string.IsNullOrEmpty(sourceBranch)
                ? $"refs/heads/{sourceBranch}"
                : null;

            return new PrReviewScriptInput(platformName, repoUrl!, prNumber, TenantId: "", SourceRef: sourceRef);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM PR extraction failed.");
            return null;
        }
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
