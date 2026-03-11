using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace AgentTeam.Console.Workflows;

/// <summary>
/// Per-PR workflow that runs run-pr-review.sh for a single PR.
/// Started by the integrator on each webhook; receives PR context and executes the script via activity.
/// </summary>
[Workflow("PR Review Agent:PR Review Script Workflow")]
public class PrReviewScriptWorkflow
{

    /// <summary>
    /// Runs the PR review script for the given PR. One execution per webhook.
    /// </summary>
    [WorkflowRun]
    public async Task RunAsync(PrReviewScriptInput input)
    {
        try
        {
            Workflow.Logger.LogInformation(
                "Running PR review script for {Repo}#{PrNumber} (platform: {Platform})",
                input.RepoUrl, input.PrNumber, input.PlatformName);

            await Workflow.ExecuteActivityAsync(
                (RunPrReviewScriptActivity a) => a.RunAsync(input),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(15) });
        }
        catch (ActivityFailureException ex)
        {
            Workflow.Logger.LogWarning(
                "PR review failed for {Repo}#{PrNumber}: {Message}",
                input.RepoUrl, input.PrNumber, ex.Message);
        }
        catch (Exception ex)
        {
            Workflow.Logger.LogError(ex,
                "PR review failed for {Repo}#{PrNumber}",
                input.RepoUrl, input.PrNumber);
        }
    }

}
