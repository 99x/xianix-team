using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Xians.Lib.Agents.Core;
using Xians.Lib.Logging;

namespace AgentTeam.Console.Workflows;

/// <summary>
/// Temporal activity that runs the run-requirement-analysis.sh script with the given issue context.
/// </summary>
public class RunRequirementAnalysisActivity
{
    private static readonly ILogger Logger = XiansLogger.GetLogger<RunRequirementAnalysisActivity>();

    [Activity("RunRequirementAnalysis")]
    public async Task<int> RunAsync(RequirementAnalysisInput input)
    {
        await XiansContext.Metrics
            .WithCustomIdentifier($"{input.PlatformName}:{input.RepoUrl}#{input.IssueNumber}")
            .WithMetadata("platform", input.PlatformName)
            .WithMetadata("repo", input.RepoUrl)
            .WithMetric("requirement_analyses", "started", 1, "count")
            .ReportAsync();

        var repoRoot = ResolveRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "run-requirement-analysis.sh");

        Logger.LogInformation(
            "Starting requirement analysis for {Platform} {Repo}#{IssueNumber} (script: {ScriptPath})",
            input.PlatformName, input.RepoUrl, input.IssueNumber, scriptPath);

        if (!File.Exists(scriptPath))
        {
            Logger.LogError(
                "run-requirement-analysis.sh not found at {ScriptPath}. Set XIANIX_REPO_ROOT to repo root if needed.", scriptPath);
            await XiansContext.Metrics
                .WithCustomIdentifier($"{input.PlatformName}:{input.RepoUrl}#{input.IssueNumber}")
                .WithMetadata("platform", input.PlatformName)
                .WithMetadata("repo", input.RepoUrl)
                .WithMetric("requirement_analyses", "failed", 1, "count")
                .ReportAsync();
            return 1;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            ArgumentList = { scriptPath },
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.Environment["PLATFORM"] = input.PlatformName;
        startInfo.Environment["REPO_URL"] = input.RepoUrl;
        startInfo.Environment["ISSUE_NUMBER"] = input.IssueNumber.ToString();

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Logger.LogError("Failed to start run-requirement-analysis.sh");
            await XiansContext.Metrics
                .WithCustomIdentifier($"{input.PlatformName}:{input.RepoUrl}#{input.IssueNumber}")
                .WithMetadata("platform", input.PlatformName)
                .WithMetadata("repo", input.RepoUrl)
                .WithMetric("requirement_analyses", "failed", 1, "count")
                .ReportAsync();
            return 1;
        }

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Logger.LogInformation("[run-requirement-analysis] {Line}", e.Data);
                System.Console.Out.WriteLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Logger.LogInformation("[run-requirement-analysis stderr] {Line}", e.Data);
                System.Console.Error.WriteLine(e.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        Logger.LogInformation(
            "Requirement analysis script finished for {Repo}#{IssueNumber} (exit code: {ExitCode})",
            input.RepoUrl, input.IssueNumber, process.ExitCode);

        var metricName = process.ExitCode == 0 ? "completed" : "failed";
        await XiansContext.Metrics
            .WithCustomIdentifier($"{input.PlatformName}:{input.RepoUrl}#{input.IssueNumber}")
            .WithMetadata("platform", input.PlatformName)
            .WithMetadata("repo", input.RepoUrl)
            .WithMetric("requirement_analyses", metricName, 1, "count")
            .ReportAsync();

        if (process.ExitCode != 0)
        {
            Logger.LogWarning(
                "[run-requirement-analysis] Exited with code {ExitCode}", process.ExitCode);
        }

        return process.ExitCode;
    }

    private static string ResolveRepoRoot()
    {
        if (Environment.GetEnvironmentVariable("XIANIX_REPO_ROOT") is { Length: > 0 } envRoot)
        {
            var path = Path.GetFullPath(envRoot);
            if (File.Exists(Path.Combine(path, "scripts", "run-requirement-analysis.sh")))
                return path;
        }

        foreach (var startDir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = Path.GetFullPath(startDir);
            while (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                var scriptPath = Path.Combine(dir, "scripts", "run-requirement-analysis.sh");
                if (File.Exists(scriptPath))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
