using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Auditing;

/// <summary>
/// An immutable, append-only record of a single business mutation: who did it, what they did,
/// which entity it affected, when, and a permitted summary of the change.
/// </summary>
/// <remarks>
/// <para>
/// Every business mutation records an <see cref="AuditEntry"/> capturing the actor, action,
/// affected entity, timestamp, and a permitted change summary (requirement 6.7 / A4). The entry
/// is written within the same transaction as the mutation it describes by a later task; this
/// type declares only the stored shape.
/// </para>
/// <para>
/// It derives from <see cref="Entity"/> (not <see cref="VersionedEntity"/>): an audit entry is
/// inserted once and never updated, so it needs no concurrency token. The audit trail must never
/// be cascade-deleted — the model-wide <c>DeleteBehavior.Restrict</c> default enforces that.
/// </para>
/// <para>
/// <b>Actor is a plain GUID.</b> The authenticated staff user is identified by
/// <see cref="ActorId"/>, a bare <see cref="Guid"/> with no foreign key to the ASP.NET Core
/// Identity tables (configured in task 4.1). A background-worker system actor is represented by
/// the same column with a reserved system identifier, so worker mutations are audited the same
/// way staff mutations are. <see cref="PermittedChangeSummary"/> holds only an allowlisted,
/// human-readable description of the change — never secrets or unnecessary personal data.
/// </para>
/// </remarks>
public class AuditEntry : Entity
{
    /// <summary>
    /// The actor who performed the mutation: an authenticated staff user id, or a reserved
    /// system identifier for a background-worker action. A plain GUID with no Identity foreign
    /// key in Phase A.
    /// </summary>
    public Guid ActorId { get; set; }

    /// <summary>The action performed (for example, "VariantCreated" or "PieceArchived").</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The type name of the affected entity (for example, "ProductVariant").</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>The identifier of the affected entity instance.</summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// The instant the mutation occurred, as a UTC <see cref="DateTimeOffset"/>. Obtained through
    /// an injected <c>TimeProvider</c> rather than the system clock in business code.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// A permitted, human-readable summary of what changed. Contains only allowlisted,
    /// non-sensitive information — never secrets or unnecessary personal data.
    /// </summary>
    public string PermittedChangeSummary { get; set; } = string.Empty;
}
