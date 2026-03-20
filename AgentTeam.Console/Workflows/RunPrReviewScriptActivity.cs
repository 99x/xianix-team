using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Xians.Lib.Logging;

namespace AgentTeam.Console.Workflows;

/// <summary>
/// Temporal activity that runs the run-pr-review.sh script with the given PR context.
/// </summary>
public class RunPrReviewScriptActivity
{
    private static readonly ILogger Logger = XiansLogger.GetLogger<RunPrReviewScriptActivity>();

    [Activity("RunPrReviewScript")]
    public async Task RunAsync(PrReviewScriptInput input)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;

        var repoRoot = ResolveRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "run-pr-review.sh");

        Logger.LogInformation(
            "Starting PR review script for {Platform} {Repo}#{PrNumber} (script: {ScriptPath})",
            input.PlatformName, input.RepoUrl, input.PrNumber, scriptPath);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException(
                $"run-pr-review.sh not found at '{scriptPath}'. Set XIANIX_REPO_ROOT env var to the repo root directory.",
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

        // Tenant-scoped directories for isolation when multiple tenants share the same process
        if (!string.IsNullOrEmpty(input.TenantId))
        {
            var baseCache = Environment.GetEnvironmentVariable("PR_REVIEW_CACHE_BASE") ?? "/tmp/pr-review-cache";
            var tenantSafe = SanitizeForPath(input.TenantId);
            var repoSlug = DeriveRepoSlug(input.RepoUrl);
            startInfo.Environment["REPO_CACHE_DIR"] = Path.Combine(baseCache, tenantSafe, repoSlug);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var unique = Guid.NewGuid().ToString("N")[..6];
            startInfo.Environment["WORKDIR"] = $"/tmp/pr-review-{tenantSafe}-{input.PrNumber}-{timestamp}-{unique}";
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start run-pr-review.sh process.");

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                Logger.LogInformation("[run-pr-review] {Line}", e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                Logger.LogWarning("[run-pr-review stderr] {Line}", e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        Logger.LogInformation(
            "PR review script finished for {Repo}#{PrNumber} (exit code: {ExitCode})",
            input.RepoUrl, input.PrNumber, process.ExitCode);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"run-pr-review.sh exited with code {process.ExitCode} for {input.PlatformName} PR #{input.PrNumber} ({input.RepoUrl}).");
        }
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
                if (File.Exists(Path.Combine(dir, "scripts", "run-pr-review.sh")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
        }

        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Derives a filesystem-safe slug from a repo URL (matches run-pr-review.sh logic).
    /// e.g. https://github.com/org/repo.git → github.com-org-repo
    /// </summary>
    private static string DeriveRepoSlug(string repoUrl)
    {
        var s = repoUrl
            .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase);
        if (s.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            s = s[..^4];
        s = Regex.Replace(s, @"[/: ]", "-");
        s = Regex.Replace(s, @"%[0-9A-Fa-f]{2}", "-");
        return s.Trim('-');
    }

    /// <summary>
    /// Sanitizes a string for use in filesystem paths (alphanumeric, hyphens, underscores).
    /// </summary>
    private static string SanitizeForPath(string value)
    {
        var sanitized = Regex.Replace(value, @"[^a-zA-Z0-9\-_.]", "-");
        return sanitized.Trim('-');
    }
}
