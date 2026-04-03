using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AgentTeam.Console.Rules;

/// <summary>
/// Evaluates incoming webhook payloads against declarative rules in <c>agents.json</c>
/// (<c>whenAll</c>: path + <c>equals</c> / <c>exists</c> / <c>in</c> / <c>arrayAny</c>).
/// Configuration is loaded once on first use: an <c>agents.json</c> next to the executable overrides
/// the copy embedded in the assembly; if neither file exists, the embedded default is used.
/// </summary>
public static class WebhookRuleEvaluator
{
    private static readonly JsonSerializerOptions ConfigDeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Lazy<List<AgentWebhookConfiguration>> Agents = new(LoadAgents);

    public static Task<bool> ShouldProcessAsync(string agentName, string payloadJson, ILogger? logger = null)
    {
        JsonNode? payload;
        try
        {
            payload = JsonNode.Parse(payloadJson);
        }
        catch (JsonException)
        {
            return Task.FromResult(false);
        }

        if (payload is null) return Task.FromResult(false);

        var agent = Agents.Value.Find(a =>
            string.Equals(a.AgentName, agentName, StringComparison.OrdinalIgnoreCase));
        if (agent is null) return Task.FromResult(false);

        foreach (var rule in agent.WebhookFilterRules)
        {
            if (rule.WhenAll is null || rule.WhenAll.Count == 0) continue;

            var ok = MatchAll(payload, rule.WhenAll);
            logger?.LogDebug("Rule '{RuleName}': {Outcome}", rule.RuleName, ok ? "PASSED" : "FAILED");
            if (ok) return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    private static bool MatchAll(JsonNode root, IReadOnlyList<WebhookCondition> conditions)
    {
        foreach (var c in conditions)
        {
            if (!MatchOne(root, c)) return false;
        }

        return true;
    }

    private static bool MatchOne(JsonNode root, WebhookCondition c)
    {
        if (string.IsNullOrEmpty(c.Path)) return false;

        if (c.ArrayAny is not null)
            return MatchArrayAny(root, c.Path, c.ArrayAny);

        if (c.Exists.HasValue)
            return c.Exists.Value ? NodeExists(root, c.Path) : !NodeExists(root, c.Path);

        if (c.In is { Count: > 0 })
        {
            var t = GetText(root, c.Path);
            return t is not null && c.In.Any(v => string.Equals(v, t, StringComparison.Ordinal));
        }

        if (c.EqualsValue is not null)
            return string.Equals(GetText(root, c.Path), c.EqualsValue, StringComparison.Ordinal);

        return false;
    }

    private static bool MatchArrayAny(JsonNode root, string arrayPath, WebhookArrayAny ax)
    {
        var arrNode = GetNode(root, arrayPath);
        if (arrNode is not JsonArray arr) return false;
        foreach (var item in arr)
        {
            if (item is not JsonObject o) continue;
            if (!o.TryGetPropertyValue(ax.Property, out var prop) || prop is not JsonValue lv) continue;
            var s = lv.TryGetValue<string>(out var t) ? t : lv.ToString();
            if (string.Equals(s, ax.EqualsValue, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static bool NodeExists(JsonNode root, string path)
    {
        var n = GetNode(root, path);
        if (n is null) return false;
        if (n is JsonValue v)
        {
            try
            {
                if (v.GetValueKind() == JsonValueKind.Null) return false;
            }
            catch
            {
                /* ignore */
            }
        }

        return true;
    }

    private static string? GetText(JsonNode root, string path)
    {
        var node = GetNode(root, path);
        return node switch
        {
            null => null,
            JsonValue v => v.TryGetValue<string>(out var s) ? s : v.ToString(),
            _ => node.ToString()
        };
    }

    private static JsonNode? GetNode(JsonNode root, string path)
    {
        JsonNode? current = root;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is JsonObject o && o.TryGetPropertyValue(part, out var next))
                current = next;
            else
                return null;
        }

        return current;
    }

    private static List<AgentWebhookConfiguration> LoadAgents()
    {
        var json = ReadAgentsConfigurationJson();
        var list = JsonSerializer.Deserialize<List<AgentWebhookConfiguration>>(json, ConfigDeserializeOptions);
        return list ?? throw new InvalidOperationException("agents.json must be a non-empty JSON array of agent configurations.");
    }

    private static string ReadAgentsConfigurationJson()
    {
        var binPath = Path.Combine(AppContext.BaseDirectory, "agents.json");
        if (File.Exists(binPath))
            return File.ReadAllText(binPath);

        var projectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "agents.json"));
        if (File.Exists(projectRoot))
            return File.ReadAllText(projectRoot);

        var assembly = typeof(WebhookRuleEvaluator).Assembly;
        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith("agents.json", StringComparison.Ordinal));
        if (resourceName is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
        }

        throw new FileNotFoundException(
            "agents.json not found on disk and no embedded agents.json resource was found.");
    }
}
