using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xians.Lib.Agents.Messaging;

namespace AgentTeam.Console.Supervisor;

/// <summary>
/// Injects recent Xians thread messages (first page, <see cref="HistoryPageSize"/> max) as MAF context.
/// One instance per <see cref="MafSubAgent.RunAsync"/> call, bound to that turn's <see cref="UserMessageContext"/>.
/// </summary>
internal sealed class ChatHistoryProvider(UserMessageContext userContext) : AIContextProvider(null, null)
{
    private readonly UserMessageContext _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

    internal const int HistoryPageSize = 10;

    public override IReadOnlyList<string> StateKeys => [];

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var xiansMessages = await _userContext.GetChatHistoryAsync(page: 1, pageSize: HistoryPageSize).ConfigureAwait(false);

        var messages = xiansMessages
            .Where(msg => !string.IsNullOrEmpty(msg.Text))
            .Select(msg => new ChatMessage(
                msg.Direction.ToLowerInvariant() == "outgoing" ? ChatRole.Assistant : ChatRole.User,
                msg.Text!))
            .Reverse()
            .ToList();

        return new AIContext { Messages = messages };
    }

    protected override ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default) =>
        default;
}
