namespace PenguinPlank.Contracts.Integrations;

/// <summary>
/// The versioned transport shape for a queued sync exception and its available actions
/// (requirement R11 / 3.11, 3.12). The error text is already redacted.
/// </summary>
public sealed record SyncExceptionResponse
{
    /// <summary>The exception identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>The run that raised the error.</summary>
    public required Guid SyncRunId { get; init; }

    /// <summary>The affected resource.</summary>
    public required string AffectedResource { get; init; }

    /// <summary>The redacted error text.</summary>
    public required string RedactedError { get; init; }

    /// <summary>The resolution state as a stable string.</summary>
    public required string Status { get; init; }

    /// <summary>The instant the error was queued.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>The actions currently available, as stable strings ("Retry", "Reconcile").</summary>
    public required IReadOnlyList<string> AvailableActions { get; init; }
}
