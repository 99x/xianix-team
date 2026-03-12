namespace AgentTeam.Console.Webhooks.Models;

/// <summary>
/// Type of pull request webhook event.
/// </summary>
public enum PrWebhookEvent
{
    PullRequestCreated,
    PullRequestSynchronized,
}
