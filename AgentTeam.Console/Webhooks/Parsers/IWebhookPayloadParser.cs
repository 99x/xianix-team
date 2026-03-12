using AgentTeam.Console.Webhooks.Models;

namespace AgentTeam.Console.Webhooks.Parsers;

/// <summary>
/// Parses provider-specific webhook payloads into a unified PR context.
/// </summary>
public interface IWebhookPayloadParser
{
    /// <summary>
    /// Returns true if this parser can handle the given payload (and optional headers).
    /// </summary>
    bool CanParse(string rawPayload, IReadOnlyDictionary<string, string>? headers = null);

    /// <summary>
    /// Parses the payload into a unified PR context. Call only when CanParse returns true.
    /// </summary>
    PrWebhookContext? Parse(string rawPayload, IReadOnlyDictionary<string, string>? headers = null);
}
