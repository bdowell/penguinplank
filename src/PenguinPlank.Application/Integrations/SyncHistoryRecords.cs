using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Application.Integrations;

/// <summary>A sync run about to be recorded.</summary>
/// <param name="Id">The run identifier.</param>
/// <param name="ConnectionId">The owning connection.</param>
/// <param name="Resource">The resource being synchronized.</param>
/// <param name="StartedAtUtc">The start instant.</param>
public sealed record NewSyncRun(Guid Id, Guid ConnectionId, string Resource, DateTimeOffset StartedAtUtc);

/// <summary>The final counts and timing of a sync run.</summary>
/// <param name="RunId">The run being completed.</param>
/// <param name="ProcessedCount">Items processed.</param>
/// <param name="FailedCount">Items that failed.</param>
/// <param name="CompletedAtUtc">The completion instant.</param>
/// <param name="LastSuccessAtUtc">The last-success instant to record, or <see langword="null"/> when the run did not succeed.</param>
public sealed record SyncRunCompletion(
    Guid RunId,
    int ProcessedCount,
    int FailedCount,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset? LastSuccessAtUtc);

/// <summary>A permanent error about to be queued. The error text must already be redacted.</summary>
/// <param name="Id">The exception identifier.</param>
/// <param name="SyncRunId">The run that raised the error.</param>
/// <param name="AffectedResource">The affected resource.</param>
/// <param name="RedactedError">The redacted error text.</param>
/// <param name="CreatedAtUtc">The instant the error was queued.</param>
public sealed record SyncExceptionDraft(
    Guid Id,
    Guid SyncRunId,
    string AffectedResource,
    string RedactedError,
    DateTimeOffset CreatedAtUtc);

/// <summary>A queued sync exception as stored.</summary>
/// <param name="Id">The exception identifier.</param>
/// <param name="SyncRunId">The run that raised the error.</param>
/// <param name="AffectedResource">The affected resource.</param>
/// <param name="RedactedError">The redacted error text.</param>
/// <param name="Status">The resolution state.</param>
/// <param name="CreatedAtUtc">The instant the error was queued.</param>
public sealed record SyncExceptionRecord(
    Guid Id,
    Guid SyncRunId,
    string AffectedResource,
    string RedactedError,
    SyncExceptionStatus Status,
    DateTimeOffset CreatedAtUtc);

/// <summary>Headline sync-history figures for one connection (requirement 3.12).</summary>
/// <param name="ConnectionId">The connection.</param>
/// <param name="IsPaused">Whether the connection is paused.</param>
/// <param name="LastSuccessAtUtc">The most recent successful sync instant, if any.</param>
/// <param name="BacklogCount">Outgoing jobs still pending or awaiting retry.</param>
/// <param name="FailedItemCount">Exceptions not yet resolved.</param>
public sealed record SyncHistorySummary(
    Guid ConnectionId,
    bool IsPaused,
    DateTimeOffset? LastSuccessAtUtc,
    int BacklogCount,
    int FailedItemCount);

/// <summary>An exception together with the actions the Owner can take on it.</summary>
/// <param name="Exception">The stored exception.</param>
/// <param name="AvailableActions">The retry or reconcile actions currently available.</param>
public sealed record SyncExceptionView(
    SyncExceptionRecord Exception,
    IReadOnlyList<SyncExceptionAction> AvailableActions);

/// <summary>What an Owner sees when viewing sync history (requirement 3.12).</summary>
/// <param name="Summary">The headline figures, including pause state.</param>
/// <param name="Exceptions">The unresolved exceptions with their actions.</param>
public sealed record SyncHistoryView(
    SyncHistorySummary Summary,
    IReadOnlyList<SyncExceptionView> Exceptions);

/// <summary>The fields that decide whether outgoing work may start.</summary>
/// <param name="Enabled">Whether the connection is enabled.</param>
/// <param name="IsPaused">Whether the connection is paused.</param>
public sealed record OutgoingWorkState(bool Enabled, bool IsPaused);
