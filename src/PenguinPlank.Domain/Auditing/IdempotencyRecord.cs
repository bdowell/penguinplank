using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Auditing;

/// <summary>
/// A persisted record of a mutating command keyed by a caller-supplied idempotency key, used to
/// detect replays and reject conflicting reuse of the same key.
/// </summary>
/// <remarks>
/// <para>
/// A mutating API command carries an idempotency key, persisted with the caller, operation, and
/// payload hash (requirement 6.2 / A4). Reusing the same <see cref="Key"/> with an identical
/// <see cref="PayloadHash"/> replays the stored <see cref="ResultReference"/> without repeating
/// the effect (requirement 6.4); reusing the same key with a different hash is a conflict
/// (requirement 6.3). The <b>unique index on <see cref="Key"/></b> is what serializes concurrent
/// same-key requests at the database and makes the replay/conflict decision authoritative; the
/// pipeline that uses it is added by a later task (6.3).
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: an idempotency record is written once per key and read
/// back on replay — it is append-only and needs no concurrency token. The <see cref="CallerId"/>
/// is a plain GUID with no foreign key to the Identity tables in Phase A, matching
/// <see cref="AuditEntry.ActorId"/>.
/// </para>
/// </remarks>
public class IdempotencyRecord : Entity
{
    /// <summary>
    /// The caller-supplied idempotency key. <b>Unique</b> across all records (enforced by a unique
    /// index) so the same key cannot produce two records and concurrent reuse is serialized.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The authenticated caller that issued the command. A plain GUID in Phase A.</summary>
    public Guid CallerId { get; set; }

    /// <summary>The logical operation the command performed (for example, "CreateVariant").</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// A hash of the request payload. A replay with the same <see cref="Key"/> and an identical
    /// hash returns the stored result; a different hash for the same key is rejected as a conflict.
    /// </summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>
    /// A reference to the stored result of the original command, replayed on an identical-payload
    /// retry so the effect is not repeated.
    /// </summary>
    public string? ResultReference { get; set; }
}
