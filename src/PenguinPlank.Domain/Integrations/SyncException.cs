using System.Diagnostics.CodeAnalysis;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// An actionable permanent synchronization error queued for owner resolution, recording the
/// affected resource, a redacted error, and its resolution state.
/// </summary>
/// <remarks>
/// <para>
/// A permanent mapping or permission error is routed to this exception queue (requirement 3.11 /
/// R11) and surfaced in sync history with the affected resource, a <b>redacted</b> error, and the
/// available retry or reconcile action (requirement 3.12). The stored <see cref="RedactedError"/>
/// never contains secrets, credential references, or unnecessary personal data.
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: an exception is a history/queue record whose
/// <see cref="Status"/> advances as the Owner acts on it, and must never be cascade-deleted.
/// </para>
/// </remarks>
// CA1711 rationale: "SyncException" is the design's named domain entity for the actionable
// exception queue (requirement 3.11 / R11; design Section 6), not a throwable CLR type. It is a
// persisted record, never thrown or caught. Renaming would diverge from the specified domain
// vocabulary (coding-standards §4), so the analyzer rule is suppressed for this type only.
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Named domain entity for the sync exception queue (R11 3.11); it is a persisted record, not a throwable type.")]
public class SyncException : Entity
{
    /// <summary>The <see cref="SyncRun"/> that raised this exception.</summary>
    public Guid SyncRunId { get; set; }

    /// <summary>The resource affected by the error (for example, "order:12345").</summary>
    public string AffectedResource { get; set; } = string.Empty;

    /// <summary>A redacted, safe-to-display error summary — never secrets or unnecessary PII.</summary>
    public string RedactedError { get; set; } = string.Empty;

    /// <summary>The resolution state. Defaults to <see cref="SyncExceptionStatus.Open"/>.</summary>
    public SyncExceptionStatus Status { get; set; } = SyncExceptionStatus.Open;
}
