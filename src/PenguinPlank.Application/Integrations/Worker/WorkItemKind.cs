namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>The durable queue a leased work item came from.</summary>
public enum WorkItemKind
{
    /// <summary>An inbound provider notification (<c>IntegrationInbox</c>).</summary>
    Inbox = 0,

    /// <summary>An outgoing integration job (<c>IntegrationOutbox</c>).</summary>
    Outbox = 1,
}
