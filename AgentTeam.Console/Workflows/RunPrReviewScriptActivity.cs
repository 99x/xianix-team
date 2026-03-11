using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace AgentTeam.Console.Workflows;

/// <summary>
/// Temporal activity that runs the run-pr-review.sh script with the given PR context.
/// </summary>
public class RunPrReviewScriptActivity
{
    [Activity("RunPrReviewScript")]
    public async Task<int> RunAsync(PrReviewScriptInput input)
    {
        var repoRoot = ResolveRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "run-pr-review.sh");

        if (!File.Exists(scriptPath))
        {
            ActivityExecutionContext.Current.Logger.LogError(
                "run-pr-review.sh not found at {ScriptPath}. Set XIANIX_REPO_ROOT to repo root if needed.", scriptPath);
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
        startInfo.Environment["PR_NUMBER"] = input.PrNumber.ToString();

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            ActivityExecutionContext.Current.Logger.LogError("Failed to start run-pr-review.sh");
            return 1;
        }

        var logger = ActivityExecutionContext.Current.Logger;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                logger.LogInformation("[run-pr-review] {Line}", e.Data);
                System.Console.Out.WriteLine(e.Data); // ensure local visibility
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                logger.LogWarning("[run-pr-review stderr] {Line}", e.Data);
                System.Console.Error.WriteLine(e.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            ActivityExecutionContext.Current.Logger.LogWarning(
                "[run-pr-review] Exited with code {ExitCode}", process.ExitCode);
        }

        return process.ExitCode;
    }

    private static string ResolveRepoRoot()
    {
        if (Environment.GetEnvironmentVariable("XIANIX_REPO_ROOT") is { Length: > 0 } envRoot)
        {
            var path = Path.GetFullPath(envRoot);
            if (File.Exists(Path.Combine(path, "scripts", "run-pr-review.sh")))
                return path;
        }

        foreach (var startDir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = Path.GetFullPath(startDir);
            while (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                var scriptPath = Path.Combine(dir, "scripts", "run-pr-review.sh");
                if (File.Exists(scriptPath))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
