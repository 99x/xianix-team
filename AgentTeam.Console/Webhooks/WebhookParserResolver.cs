using AgentTeam.Console.Webhooks.Models;
using AgentTeam.Console.Webhooks.Parsers;

namespace AgentTeam.Console.Webhooks;

/// <summary>
/// Resolves webhook payloads to the appropriate parser and produces a unified PR context.
/// </summary>
public sealed class WebhookParserResolver
{
    private readonly IReadOnlyList<IWebhookPayloadParser> _parsers;

    public WebhookParserResolver(params IWebhookPayloadParser[] parsers)
    {
        _parsers = parsers.Length > 0 ? parsers : [new GitHubWebhookParser(), new AzureDevOpsWebhookParser()];
    }

    /// <summary>
    /// Tries to find a parser that can handle the payload.
    /// </summary>
    public IWebhookPayloadParser? TryResolve(string rawPayload, IReadOnlyDictionary<string, string>? headers = null)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return null;

        return _parsers.FirstOrDefault(p => p.CanParse(rawPayload, headers));
    }

    /// <summary>
    /// Parses the payload using the first matching parser. Returns null if no parser matches or parsing fails.
    /// </summary>
    public PrWebhookContext? Parse(string rawPayload, IReadOnlyDictionary<string, string>? headers = null)
    {
        var parser = TryResolve(rawPayload, headers);
        return parser?.Parse(rawPayload, headers);
    }
}
