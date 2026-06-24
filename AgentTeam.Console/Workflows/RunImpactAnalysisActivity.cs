using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace AgentTeam.Console.Workflows;

/// <summary>
/// Temporal activity that runs the run-impact-analysis.sh script with the given PR context.
/// </summary>
public class RunImpactAnalysisActivity
{
    [Activity("RunImpactAnalysis")]
    public async Task RunAsync(ImpactAnalysisInput input)
    {
        var logger = ActivityExecutionContext.Current.Logger;
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;

        var repoRoot = ResolveRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "run-impact-analysis.sh");

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException(
                $"run-impact-analysis.sh not found at '{scriptPath}'. Set XIANIX_REPO_ROOT env var to the repo root directory.",
                scriptPath);
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

        // Inherit current process env so GIT_TOKEN, AZURE_TOKEN, GITHUB_TOKEN, GIT_USER_* etc. are passed
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string k && entry.Value is string v && !string.IsNullOrEmpty(k))
                startInfo.Environment[k] = v;
        }

        startInfo.Environment["PLATFORM"] = input.PlatformName;
        startInfo.Environment["REPO_URL"] = input.RepoUrl;
        startInfo.Environment["PR_NUMBER"] = input.PrNumber.ToString();
        if (!string.IsNullOrEmpty(input.SourceRef))
            startInfo.Environment["PR_SOURCE_REF"] = input.SourceRef;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start run-impact-analysis.sh process.");

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                logger.LogInformation("[run-impact-analysis] {Line}", e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                logger.LogWarning("[run-impact-analysis stderr] {Line}", e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"run-impact-analysis.sh exited with code {process.ExitCode} for {input.PlatformName} PR #{input.PrNumber} ({input.RepoUrl}).");
        }
    }

    private static string ResolveRepoRoot()
    {
        if (Environment.GetEnvironmentVariable("XIANIX_REPO_ROOT") is { Length: > 0 } envRoot)
        {
            var path = Path.GetFullPath(envRoot);
            if (File.Exists(Path.Combine(path, "scripts", "run-impact-analysis.sh")))
                return path;
        }

        foreach (var startDir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = Path.GetFullPath(startDir);
            while (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                if (File.Exists(Path.Combine(dir, "scripts", "run-impact-analysis.sh")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
