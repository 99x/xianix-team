using AgentTeam.Console;
using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace AgentTeam.Console.Workflows;

/// <summary>
/// Per-issue workflow that runs scripts/run-req-analysis.sh for a single backlog item.
/// Started by the integrator on each webhook; receives issue context and executes the script via activity.
/// </summary>
[Workflow(AgentRegistration.Name + ":Requirement Analysis Workflow")]
public class RequirementAnalysisWorkflow
{
    /// <summary>
    /// Runs the requirement analysis script for the given issue. One execution per webhook.
    /// </summary>
    [WorkflowRun]
    public async Task RunAsync(RequirementAnalysisInput input)
    {
        try
        {
            Workflow.Logger.LogInformation(
                "Running requirement analysis for {Repo}#{IssueNumber} (platform: {Platform})",
                input.RepoUrl, input.IssueNumber, input.PlatformName);

            await Workflow.ExecuteActivityAsync(
                (RunRequirementAnalysisActivity a) => a.RunAsync(input),
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(15) });

            Workflow.Logger.LogInformation(
                "Requirement analysis completed for {Repo}#{IssueNumber}",
                input.RepoUrl, input.IssueNumber);
        }
        catch (ActivityFailureException ex)
        {
            Workflow.Logger.LogWarning(
                "Requirement analysis failed for {Repo}#{IssueNumber}: {Message}",
                input.RepoUrl, input.IssueNumber, ex.Message);
        }
        catch (Exception ex)
        {
            Workflow.Logger.LogError(ex,
                "Requirement analysis failed for {Repo}#{IssueNumber}",
                input.RepoUrl, input.IssueNumber);
        }
    }
}
