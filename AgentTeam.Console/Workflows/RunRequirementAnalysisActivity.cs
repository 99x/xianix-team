using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace AgentTeam.Console.Workflows;

/// <summary>
/// Temporal activity that runs the run-requirement-analysis.sh script with the given issue context.
/// </summary>
public class RunRequirementAnalysisActivity
{
    [Activity("RunRequirementAnalysis")]
    public async Task<int> RunAsync(RequirementAnalysisInput input)
    {
        var repoRoot = ResolveRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "run-requirement-analysis.sh");

        if (!File.Exists(scriptPath))
        {
            ActivityExecutionContext.Current.Logger.LogError(
                "run-requirement-analysis.sh not found at {ScriptPath}. Set XIANIX_REPO_ROOT to repo root if needed.", scriptPath);
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
            ActivityExecutionContext.Current.Logger.LogError("Failed to start run-requirement-analysis.sh");
            return 1;
        }

        var logger = ActivityExecutionContext.Current.Logger;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                logger.LogInformation("[run-requirement-analysis] {Line}", e.Data);
                System.Console.Out.WriteLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                logger.LogInformation("[run-requirement-analysis stderr] {Line}", e.Data);
                System.Console.Error.WriteLine(e.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            ActivityExecutionContext.Current.Logger.LogWarning(
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
