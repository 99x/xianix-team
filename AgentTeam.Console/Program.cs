using AgentTeam.Console.Agents;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;

// Load .env from project dir (works regardless of cwd when running)
var envPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env"));
if (File.Exists(envPath))
    Env.Load(envPath);
else
    Env.Load(); // Fallback: search from cwd up

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL")
    ?? throw new InvalidOperationException("XIANS_SERVER_URL not found in environment variables");
var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY")
    ?? throw new InvalidOperationException("XIANS_API_KEY not found in environment variables");

// Initialize Xians Platform
var xiansPlatform = await XiansPlatform.InitializeAsync(new()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    ServerLogLevel = LogLevel.Information,
    ConsoleLogLevel = LogLevel.Debug
});

var prReviewAgent = PrReviewAgent.Register(xiansPlatform);
var requirementAnalysisAgent = RequirementAnalysisAgent.Register(xiansPlatform);

Console.WriteLine("All agents registered successfully.");

await Task.WhenAll(
    prReviewAgent.RunAllAsync(),
    requirementAnalysisAgent.RunAllAsync()
);
