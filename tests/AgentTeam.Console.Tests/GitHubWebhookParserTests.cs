using AgentTeam.Console.Webhooks.Models;
using AgentTeam.Console.Webhooks.Parsers;
using Xunit;

namespace AgentTeam.Console.Tests;

public class GitHubWebhookParserTests
{
    private readonly GitHubWebhookParser _parser = new();

    [Fact]
    public void CanParse_ReturnsTrue_ForGitHubPullRequestPayload()
    {
        var payload = GetGitHubPrCreatedPayload();
        Assert.True(_parser.CanParse(payload));
    }

    [Fact]
    public void CanParse_ReturnsTrue_WhenXGitHubEventHeaderIsPullRequest()
    {
        var headers = new Dictionary<string, string> { ["X-GitHub-Event"] = "pull_request" };
        Assert.True(_parser.CanParse("{}", headers));
    }

    [Fact]
    public void Parse_ExtractsContext_FromGitHubPrCreatedPayload()
    {
        var payload = GetGitHubPrCreatedPayload();
        var result = _parser.Parse(payload);

        Assert.NotNull(result);
        Assert.Equal(GitProvider.GitHub, result.Platform);
        Assert.Equal(PrWebhookEvent.PullRequestCreated, result.EventType);
        Assert.Equal("https://github.com/XiansAiPlatform/agent-studio.git", result.RepoUrl);
        Assert.Equal(21, result.PrNumber);
        Assert.Equal("low-noice-view", result.SourceBranch);
        Assert.Equal("main", result.TargetBranch);
        Assert.Equal("https://github.com/XiansAiPlatform/agent-studio/pull/21.diff", result.DiffUrl);
        Assert.Equal("github", result.PlatformName);
    }

    [Fact]
    public void Parse_MapsSynchronizeAction_ToPullRequestSynchronized()
    {
        var payload = GetGitHubPrCreatedPayload().Replace("\"opened\"", "\"synchronize\"");
        var result = _parser.Parse(payload);

        Assert.NotNull(result);
        Assert.Equal(PrWebhookEvent.PullRequestSynchronized, result.EventType);
    }

    private static string GetGitHubPrCreatedPayload()
    {
        return """
        {
          "action": "opened",
          "number": 21,
          "pull_request": {
            "number": 21,
            "diff_url": "https://github.com/XiansAiPlatform/agent-studio/pull/21.diff",
            "head": { "ref": "low-noice-view" },
            "base": { "ref": "main" }
          },
          "repository": {
            "full_name": "XiansAiPlatform/agent-studio",
            "clone_url": "https://github.com/XiansAiPlatform/agent-studio.git",
            "html_url": "https://github.com/XiansAiPlatform/agent-studio"
          }
        }
        """;
    }
}
