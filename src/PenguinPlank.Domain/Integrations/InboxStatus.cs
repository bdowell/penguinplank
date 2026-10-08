namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// The processing status of an <see cref="IntegrationInbox"/> item.
/// </summary>
/// <remarks>
/// Inbound notifications are durably recorded before acknowledgment and processed asynchronously
/// (requirement 3.6 / R11). A newly recorded item is <see cref="Received"/>; the worker (added by
/// a later task) advances it. Connectors are disabled in Phase A, so no item is actually
/// processed yet.
/// </remarks>
public enum InboxStatus
{
    /// <summary>Durably recorded and awaiting processing. The default for a new item.</summary>
    Received = 0,

    /// <summary>Currently leased by a worker for processing.</summary>
    Processing = 1,

    /// <summary>Successfully processed.</summary>
    Processed = 2,

    /// <summary>Processing failed and the item is awaiting retry.</summary>
    Failed = 3,

    /// <summary>A duplicate delivery that was reconciled rather than reapplied.</summary>
    Duplicate = 4,
}
