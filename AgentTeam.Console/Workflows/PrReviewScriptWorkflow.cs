using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace AgentTeam.Console.Workflows;

/// <summary>
/// Temporal workflow that runs run-pr-review.sh for each signaled PR.
/// Receives PR webhook signals in daemon mode and executes the script via activity.
/// </summary>
[Workflow("PR Review Agent:PR Review Script Workflow")]
public class PrReviewScriptWorkflow
{
    private readonly ConcurrentQueue<PrReviewScriptInput> _pendingInputs = new();

    /// <summary>
    /// Daemon mode: waits for PR webhook signals and runs the script for each.
    /// Started with empty args, then signaled via TriggerPrReviewAsync.
    /// </summary>
    [WorkflowRun]
    public async Task RunAsync()
    {
        while (true)
        {
            await Workflow.WaitConditionAsync(() => !_pendingInputs.IsEmpty);
            if (!_pendingInputs.TryDequeue(out var input))
                continue;

            Workflow.Logger.LogInformation(
                "Running PR review script for {Repo}#{PrNumber} (platform: {Platform})",
                input.RepoUrl, input.PrNumber, input.PlatformName);

            await Workflow.ExecuteActivityAsync(
                (RunPrReviewScriptActivity a) => a.RunAsync(input),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(15) });

            if (Workflow.ContinueAsNewSuggested)
            {
                throw Workflow.CreateContinueAsNewException((PrReviewScriptWorkflow wf) => wf.RunAsync());
            }
        }
    }

    [WorkflowSignal("TriggerPrReviewAsync")]
    public Task TriggerPrReviewAsync(PrReviewScriptInput input)
    {
        _pendingInputs.Enqueue(input);
        Workflow.Logger.LogInformation(
            "Triggered PR review for {Repo}#{PrNumber} with platform {Platform}",
            input.RepoUrl, input.PrNumber, input.PlatformName);
        return Workflow.DelayAsync(TimeSpan.FromSeconds(1));
    }
}
