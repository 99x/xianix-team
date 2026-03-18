using AgentTeam.Console.Webhooks.Models;
using AgentTeam.Console.Webhooks.Parsers;

namespace AgentTeam.Console.Webhooks;

/// <summary>
/// Resolves webhook payloads to the appropriate parser and produces a unified issue context.
/// </summary>
public sealed class IssueWebhookParserResolver
{
    private readonly IReadOnlyList<IIssueWebhookPayloadParser> _parsers;

    public IssueWebhookParserResolver(params IIssueWebhookPayloadParser[] parsers)
    {
        _parsers = parsers.Length > 0 ? parsers : [new GitHubIssueWebhookParser()];
    }

    /// <summary>
    /// Tries to find a parser that can handle the payload.
    /// </summary>
    public IIssueWebhookPayloadParser? TryResolve(string rawPayload, IReadOnlyDictionary<string, string>? headers = null)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return null;

        return _parsers.FirstOrDefault(p => p.CanParse(rawPayload, headers));
    }

    /// <summary>
    /// Parses the payload using the first matching parser. Returns null if no parser matches or parsing fails.
    /// </summary>
    public IssueWebhookContext? Parse(string rawPayload, IReadOnlyDictionary<string, string>? headers = null)
    {
        var parser = TryResolve(rawPayload, headers);
        return parser?.Parse(rawPayload, headers);
    }
}
