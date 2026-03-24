using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xians.Lib.Agents.Messaging;

namespace AgentTeam.Console.Supervisor;

/// <summary>
/// Injects recent Xians thread messages (first page, <see cref="HistoryPageSize"/> max) as MAF context.
/// One instance per <see cref="MafSubAgent.RunAsync"/> call, bound to that turn's <see cref="UserMessageContext"/>.
/// </summary>
/// <remarks>
/// The framework default merges provider messages as <c>currentInput.Concat(history)</c>, so the last message seen by the
/// model was the previous assistant turn instead of the current user message. We override
/// <see cref="InvokingCoreAsync"/> to merge <c>history.Concat(currentInput)</c> (chronological: past turns, then this turn).
/// </remarks>
internal sealed class ChatHistoryProvider(UserMessageContext userContext) : AIContextProvider(null, null)
{
    private readonly UserMessageContext _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

    internal const int HistoryPageSize = 10;

    public override IReadOnlyList<string> StateKeys => [];

    protected override async ValueTask<AIContext> InvokingCoreAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        AIContext inputContext = context.AIContext;
        var filteredInput = new InvokingContext(context.Agent, context.Session, new AIContext
        {
            Instructions = inputContext.Instructions,
            Messages = inputContext.Messages is not null ? ProvideInputMessageFilter(inputContext.Messages) : null,
            Tools = inputContext.Tools
        });

        AIContext additional = await ProvideAIContextAsync(filteredInput, cancellationToken).ConfigureAwait(false);

        string? instructions = inputContext.Instructions;
        string? additionalInstructions = additional.Instructions;
        string? mergedInstructions = (instructions, additionalInstructions) switch
        {
            (null, _) => additionalInstructions,
            (_, null) => instructions,
            _ => instructions + "\n" + additionalInstructions
        };

        IEnumerable<ChatMessage>? historyStamped = additional.Messages?.Select(m =>
            m.WithAgentRequestMessageSource(AgentRequestMessageSourceType.AIContextProvider, typeof(ChatHistoryProvider).FullName));

        IEnumerable<ChatMessage>? inputMessages = inputContext.Messages;
        IEnumerable<ChatMessage>? mergedMessages = (historyStamped, inputMessages) switch
        {
            (null, _) => inputMessages,
            (_, null) => historyStamped,
            _ => historyStamped!.Concat(inputMessages!)
        };

        IEnumerable<AITool>? tools = inputContext.Tools;
        IEnumerable<AITool>? additionalTools = additional.Tools;
        IEnumerable<AITool>? mergedTools = (tools, additionalTools) switch
        {
            (null, _) => additionalTools,
            (_, null) => tools,
            _ => tools!.Concat(additionalTools!)
        };

        return new AIContext
        {
            Instructions = mergedInstructions,
            Messages = mergedMessages,
            Tools = mergedTools
        };
    }

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var xiansMessages = await _userContext.GetChatHistoryAsync(page: 1, pageSize: HistoryPageSize).ConfigureAwait(false);

        var messages = xiansMessages
            .Where(msg => !string.IsNullOrEmpty(msg.Text))
            .OrderBy(msg => msg.CreatedAt)
            .Select(msg => new ChatMessage(
                msg.Direction.ToLowerInvariant() == "outgoing" ? ChatRole.Assistant : ChatRole.User,
                msg.Text!))
            .ToList();

        return new AIContext { Messages = messages };
    }

    protected override ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default) =>
        default;
}
