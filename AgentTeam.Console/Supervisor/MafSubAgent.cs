using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Xians.Lib.Agents.Messaging;

namespace AgentTeam.Console.Supervisor;

/// <summary>
/// Per <see cref="RunAsync"/> call: builds an agent with <see cref="ChatHistoryProvider"/> for that turn's Xians context.
/// </summary>
public sealed class MafSubAgent
{
    private readonly OpenAIClient _openAi;
    private readonly string _modelName;

    public MafSubAgent(string openAiApiKey, string modelName = "gpt-4o-mini")
    {
        _openAi = new OpenAIClient(openAiApiKey);
        _modelName = modelName;
    }

    public async Task<string> RunAsync(UserMessageContext xiansContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(xiansContext);

        var text = xiansContext.Message.Text
            ?? throw new InvalidOperationException("UserMessageContext.Message.Text is required.");

        var tools = new MafSubAgentTools(xiansContext);

        var agent = _openAi.GetChatClient(_modelName).AsAIAgent(new ChatClientAgentOptions
        {
            Name = "MafSubAgent",
            ChatOptions = new ChatOptions
            {
                Instructions = "You are a friendly assistant. Keep your answers brief.",
                Tools =
                [
                    AIFunctionFactory.Create(tools.GetCurrentDateTime),
                    AIFunctionFactory.Create(tools.StartPrReviewWorkflow)
                ]
            },
            AIContextProviders = [new ChatHistoryProvider(xiansContext)]
        });

        return (await agent.RunAsync(text, cancellationToken: cancellationToken).ConfigureAwait(false)).Text;
    }
}
