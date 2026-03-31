using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace AgentTeam.Console.Workflows;

/// <summary>
/// Per-PR workflow that runs scripts/run-security-review.sh for a single pull request.
/// Started by the integrator on each webhook; receives PR context and executes the script via activity.
/// </summary>
[Workflow("Xianix Agent Team:Security Review Workflow")]
public class SecurityReviewWorkflow
{
    // Configurable via env var; defaults to 20 minutes.
    private static readonly TimeSpan ActivityTimeout = GetActivityTimeout();

    /// <summary>
    /// Runs the security review script for the given PR. One execution per webhook.
    /// </summary>
    [WorkflowRun]
    public async Task RunAsync(SecurityReviewInput input)
    {
        Workflow.Logger.LogInformation(
            "Starting security review for {Repo}#{PrNumber} (platform: {Platform})",
            input.RepoUrl, input.PrNumber, input.PlatformName);

        try
        {
            await Workflow.ExecuteActivityAsync(
                (RunSecurityReviewActivity a) => a.RunAsync(input),
                new ActivityOptions
                {
                    StartToCloseTimeout = ActivityTimeout,
                    RetryPolicy = new RetryPolicy
                    {
                        // Script runs are not idempotent by default; do not auto-retry.
                        MaximumAttempts = 1,
                    },
                });

            Workflow.Logger.LogInformation(
                "Security review completed for {Repo}#{PrNumber}",
                input.RepoUrl, input.PrNumber);
        }
        catch (ActivityFailureException ex)
        {
            Workflow.Logger.LogError(ex,
                "Security review failed for {Repo}#{PrNumber}: {Message}",
                input.RepoUrl, input.PrNumber, ex.Message);
            throw;
        }
    }

    private static TimeSpan GetActivityTimeout()
    {
        if (Environment.GetEnvironmentVariable("SECURITY_REVIEW_TIMEOUT_MINUTES") is { Length: > 0 } raw
            && int.TryParse(raw, out var minutes) && minutes > 0)
            return TimeSpan.FromMinutes(minutes);
        return TimeSpan.FromMinutes(20);
    }
}
