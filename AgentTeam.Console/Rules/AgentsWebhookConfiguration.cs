using System.Text.Json.Serialization;

namespace AgentTeam.Console.Rules;

public sealed class AgentWebhookConfiguration
{
    public string AgentName { get; set; } = "";
    public List<WebhookFilterRuleDefinition> WebhookFilterRules { get; set; } = [];
}

public sealed class WebhookFilterRuleDefinition
{
    public string RuleName { get; set; } = "";
    public List<WebhookCondition>? WhenAll { get; set; }
}

/// <summary>
/// One predicate. Use a single kind: <c>equals</c>, <c>exists</c>, <c>in</c>, or <c>arrayAny</c>.
/// </summary>
public sealed class WebhookCondition
{
    public string Path { get; set; } = "";

    [JsonPropertyName("equals")]
    public string? EqualsValue { get; set; }

    public bool? Exists { get; set; }

    public List<string>? In { get; set; }

    public WebhookArrayAny? ArrayAny { get; set; }
}

public sealed class WebhookArrayAny
{
    public string Property { get; set; } = "";

    [JsonPropertyName("equals")]
    public string EqualsValue { get; set; } = "";
}
