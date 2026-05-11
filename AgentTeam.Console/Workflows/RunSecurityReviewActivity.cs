using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Xians.Lib.Agents.Core;
using Xians.Lib.Logging;

namespace AgentTeam.Console.Workflows;

/// <summary>
/// Temporal activity that runs scripts/run-security-review.sh with the given PR context.
/// </summary>
public class RunSecurityReviewActivity
{
    private static readonly ILogger Logger = XiansLogger.GetLogger<RunSecurityReviewActivity>();

    [Activity("RunSecurityReview")]
    public async Task RunAsync(SecurityReviewInput input)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;

        await XiansContext.Metrics
            .WithCustomIdentifier($"{input.PlatformName}:{input.RepoUrl}#{input.PrNumber}")
            .WithMetadata("platform", input.PlatformName)
            .WithMetadata("repo", input.RepoUrl)
            .WithMetric("security_reviews", "started", 1, "count")
            .ReportAsync();

        var repoRoot = ResolveRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "run-security-review.sh");

        Logger.LogInformation(
            "Starting security review script for {Platform} {Repo}#{PrNumber} (script: {ScriptPath})",
            input.PlatformName, input.RepoUrl, input.PrNumber, scriptPath);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException(
                $"run-security-review.sh not found at '{scriptPath}'. Set XIANIX_REPO_ROOT env var to the repo root directory.",
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

        // Inherit current process env so GITHUB_TOKEN, AZURE_TOKEN, GIT_USER_* etc. are passed through
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
            var baseCache = Environment.GetEnvironmentVariable("SECURITY_REVIEW_CACHE_BASE") ?? "/tmp/security-review-cache";
            var tenantSafe = SanitizeForPath(input.TenantId);
            var repoSlug = DeriveRepoSlug(input.RepoUrl);
            startInfo.Environment["REPO_CACHE_DIR"] = Path.Combine(baseCache, tenantSafe, repoSlug);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var unique = Guid.NewGuid().ToString("N")[..6];
            startInfo.Environment["WORKDIR"] = $"/tmp/security-review-{tenantSafe}-{input.PrNumber}-{timestamp}-{unique}";
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start run-security-review.sh process.");

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                Logger.LogInformation("[run-security-review] {Line}", e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                Logger.LogWarning("[run-security-review stderr] {Line}", e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        Logger.LogInformation(
            "Security review script finished for {Repo}#{PrNumber} (exit code: {ExitCode})",
            input.RepoUrl, input.PrNumber, process.ExitCode);

        if (process.ExitCode != 0)
        {
            await XiansContext.Metrics
                .WithCustomIdentifier($"{input.PlatformName}:{input.RepoUrl}#{input.PrNumber}")
                .WithMetadata("platform", input.PlatformName)
                .WithMetadata("repo", input.RepoUrl)
                .WithMetric("security_reviews", "failed", 1, "count")
                .ReportAsync();
            throw new InvalidOperationException(
                $"run-security-review.sh exited with code {process.ExitCode} for {input.PlatformName} PR #{input.PrNumber} ({input.RepoUrl}).");
        }

        await XiansContext.Metrics
            .WithCustomIdentifier($"{input.PlatformName}:{input.RepoUrl}#{input.PrNumber}")
            .WithMetadata("platform", input.PlatformName)
            .WithMetadata("repo", input.RepoUrl)
            .WithMetric("security_reviews", "completed", 1, "count")
            .ReportAsync();
    }

    private static string ResolveRepoRoot()
    {
        if (Environment.GetEnvironmentVariable("XIANIX_REPO_ROOT") is { Length: > 0 } envRoot)
        {
            var path = Path.GetFullPath(envRoot);
            if (File.Exists(Path.Combine(path, "scripts", "run-security-review.sh")))
                return path;
        }

        foreach (var startDir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = Path.GetFullPath(startDir);
            while (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                if (File.Exists(Path.Combine(dir, "scripts", "run-security-review.sh")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
        }

        return Directory.GetCurrentDirectory();
    }

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

    private static string SanitizeForPath(string value)
    {
        var sanitized = Regex.Replace(value, @"[^a-zA-Z0-9\-_.]", "-");
        return sanitized.Trim('-');
    }
}
