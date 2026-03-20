using AgentTeam.Console.Agents;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;
using AgentTeam.Console.Workflows;

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

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

// Initialize Xians Platform. ServerLogLevel.Information enables workflow/activity log upload to Xians server.
var xiansPlatform = await XiansPlatform.InitializeAsync(new()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    ServerLogLevel = LogLevel.Information,
    ConsoleLogLevel = LogLevel.Debug
});

// PR Review Agent: registration and webhook listener (invoked when webhook name is "pr-reviewer")
using var prReviewLoggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
var prReviewLogger = prReviewLoggerFactory.CreateLogger("PrReviewAgent");

var xianixAgent = xiansPlatform.Agents.Register(new()
{
    Name = "Xianix Agent Team",
    Category = "AI-DLC",
    Summary = "A coordinated mesh of AI agents across the full software development lifecycle.",
    Description = "A coordinated mesh of AI agents across the full software development lifecycle.",
    Version = "1.0.0",
    Author = "99x",
    IsTemplate = true
});

xianixAgent.Workflows.DefineCustom<PrReviewScriptWorkflow>(new WorkflowOptions { Activable = false })
    .AddActivity<RunPrReviewScriptActivity>();

var integratorWorkflow = xianixAgent.Workflows.DefineIntegrator();
integratorWorkflow.OnWebhook(async (context) =>
{
    if (!string.Equals(context.Webhook.Name, "pr-reviewer", StringComparison.OrdinalIgnoreCase))
        return;
    await PrReviewAgent.HandleWebhookAsync(context, prReviewLogger);
});

var requirementAnalysisAgent = RequirementAnalysisAgent.Register(xiansPlatform);

Console.WriteLine("PR Review Agent registered. Listening for webhooks...");

await Task.WhenAll(
    xianixAgent.RunAllAsync(),
    requirementAnalysisAgent.RunAllAsync()
);
