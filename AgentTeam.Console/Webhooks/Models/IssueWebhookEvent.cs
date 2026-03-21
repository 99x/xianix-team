namespace AgentTeam.Console.Webhooks.Models;

/// <summary>
/// Type of issue webhook event.
/// </summary>
public enum IssueWebhookEvent
{
    IssueOpened,
    IssueEdited,
    IssueLabeled,
}
