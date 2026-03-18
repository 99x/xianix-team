using AgentTeam.Console.Webhooks.Models;

namespace AgentTeam.Console.Webhooks.Parsers;

/// <summary>
/// Parses provider-specific webhook payloads into a unified issue context.
/// </summary>
public interface IIssueWebhookPayloadParser
{
    /// <summary>
    /// Returns true if this parser can handle the given payload (and optional headers).
    /// </summary>
    bool CanParse(string rawPayload, IReadOnlyDictionary<string, string>? headers = null);

    /// <summary>
    /// Parses the payload into a unified issue context. Call only when CanParse returns true.
    /// </summary>
    IssueWebhookContext? Parse(string rawPayload, IReadOnlyDictionary<string, string>? headers = null);
}
