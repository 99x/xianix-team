using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace AgentTeam.Console.Workflows;

/// <summary>
/// Per-PR workflow that runs run-impact-analysis.sh for a single PR.
/// Started by the integrator on each webhook; receives PR context and executes the script via activity.
/// </summary>
[Workflow("Impact Analysis Agent:Impact Analysis Workflow")]
public class ImpactAnalysisWorkflow
{
    // Configurable via env var; defaults to 20 minutes to give scripts headroom.
    private static readonly TimeSpan ActivityTimeout = GetActivityTimeout();

    /// <summary>
    /// Runs the impact analysis script for the given PR. One execution per webhook.
    /// </summary>
    [WorkflowRun]
    public async Task RunAsync(ImpactAnalysisInput input)
    {
        Workflow.Logger.LogInformation(
            "Starting impact analysis for {Repo}#{PrNumber} (platform: {Platform})",
            input.RepoUrl, input.PrNumber, input.PlatformName);

        try
        {
            await Workflow.ExecuteActivityAsync(
                (RunImpactAnalysisActivity a) => a.RunAsync(input),
                new ActivityOptions
                {
                    StartToCloseTimeout = ActivityTimeout,
                    RetryPolicy = new RetryPolicy
                    {
                        MaximumAttempts = 1,
                    },
                });

            Workflow.Logger.LogInformation(
                "Impact analysis completed for {Repo}#{PrNumber}",
                input.RepoUrl, input.PrNumber);
        }
        catch (ActivityFailureException ex)
        {
            Workflow.Logger.LogError(ex,
                "Impact analysis failed for {Repo}#{PrNumber}: {Message}",
                input.RepoUrl, input.PrNumber, ex.Message);
            throw;
        }
    }

    private static TimeSpan GetActivityTimeout()
    {
        if (Environment.GetEnvironmentVariable("IMPACT_ANALYSIS_TIMEOUT_MINUTES") is { Length: > 0 } raw
            && int.TryParse(raw, out var minutes) && minutes > 0)
            return TimeSpan.FromMinutes(minutes);
        return TimeSpan.FromMinutes(20);
    }
}
