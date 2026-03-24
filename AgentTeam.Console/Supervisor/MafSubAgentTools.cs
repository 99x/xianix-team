using System.ComponentModel;
using AgentTeam.Console.Agents;
using AgentTeam.Console.Webhooks.Models;
using AgentTeam.Console.Workflows;
using Xians.Lib.Agents.Messaging;

namespace AgentTeam.Console.Supervisor;

/// <summary>
/// Instance-based AI tools for <see cref="MafSubAgent"/>, bound to the current <see cref="UserMessageContext"/>.
/// </summary>
public sealed class MafSubAgentTools
{
    private readonly UserMessageContext _context;

    public MafSubAgentTools(UserMessageContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    [Description("Get the current date and time.")]
    public async Task<string> GetCurrentDateTime()
    {
        await _context.ReplyAsync($"The current date and time is: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").ConfigureAwait(false);
        var now = DateTime.Now;
        return $"The current date and time is: {now:yyyy-MM-dd HH:mm:ss}";
    }

    [Description(
        "Starts the automated PR review workflow for a repository and pull request number. Use when the user asks to review a PR, run the PR reviewer, or start a review for a given repo.")]
    public async Task<string> StartPrReviewWorkflow(
        [Description("Source control platform: GitHub or AzureDevOps.")]
        GitProvider platform,
        [Description("Repository URL (HTTPS clone URL).")]
        string repoUrl,
        [Description("Pull request number.")]
        int prNumber)
    {
        var tenantId = _context.Message.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
            throw new InvalidOperationException("TenantId is required to start the PR review workflow.");

        var platformName = platform.ToWorkflowPlatformName();
        var input = new PrReviewScriptInput(platformName, repoUrl, prNumber, tenantId);

        await PrReviewAgent.StartReviewAsync(input).ConfigureAwait(false);

        var msg = $"Started PR review workflow for {repoUrl} PR #{prNumber} ({platformName}).";
        await _context.ReplyAsync(msg).ConfigureAwait(false);
        return msg;
    }
}
