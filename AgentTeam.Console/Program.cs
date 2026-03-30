using AgentTeam.Console;
using AgentTeam.Console.Agents;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;
using AgentTeam.Console.Workflows;
using AgentTeam.Console.Supervisor;

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
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY not found in environment variables");

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

// Agent registration and webhook listener (each agent skips unless agents.json rules match)
using var agentLoggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
var agentLogger = agentLoggerFactory.CreateLogger("XianixAgent");

var xianixAgent = xiansPlatform.Agents.Register(new()
{
    Name = AgentRegistration.Name,
    Category = AgentRegistration.Category,
    Summary = "A coordinated mesh of AI agents across the full software development lifecycle.",
    Description = "A coordinated mesh of AI agents across the full software development lifecycle.",
    Version = "1.0.0",
    Author = "99x",
    IsTemplate = true
});

xianixAgent.Workflows.DefineCustom<PrReviewScriptWorkflow>(new WorkflowOptions { Activable = false })
    .AddActivity<RunPrReviewScriptActivity>();
xianixAgent.Workflows.DefineCustom<RequirementAnalysisWorkflow>(new WorkflowOptions { Activable = false })
    .AddActivity<RunRequirementAnalysisActivity>();

var integratorWorkflow = xianixAgent.Workflows.DefineIntegrator();
integratorWorkflow.OnWebhook(async (context) =>
{
    await PrReviewerAgent.HandleWebhookAsync(context, agentLogger, openAiApiKey);
    await ReqAnalystAgent.HandleWebhookAsync(context, agentLogger, openAiApiKey);
});

var supervisor = new MafSubAgent(openAiApiKey);

var conversationalWorkflow = xianixAgent.Workflows.DefineSupervisor();
conversationalWorkflow.OnUserChatMessage(async (message) =>
{
    var reply = await supervisor.RunAsync(message, cts.Token).ConfigureAwait(false);
    await message.ReplyAsync(reply).ConfigureAwait(false);
});

Console.WriteLine("Agents registered. Listening for webhooks (PR + requirement analysis)...");

await xianixAgent.RunAllAsync();
