namespace PenguinPlank.Contracts.Administration;

/// <summary>
/// The versioned transport shape returned for one audit-trail entry on the Owner-only
/// <c>/audit</c> surface (requirement A2 §5.12; A4 §6.7).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO (dependency rule; coding-standards §3). It carries only the permitted,
/// non-sensitive summary — the actor, action, affected entity, timestamp (with offset), and a
/// change summary that lists changed property <em>names</em> only, never values, secrets, or
/// unnecessary personal data (requirement A4 §6.7).
/// </remarks>
public sealed record AuditEntryResponse
{
    /// <summary>The audit entry's stable identifier.</summary>
    public required Guid AuditEntryId { get; init; }

    /// <summary>The actor who performed the mutation (a staff user id or the reserved system id).</summary>
    public required Guid ActorId { get; init; }

    /// <summary>The action performed (for example "ProductVariantUpdated").</summary>
    public required string Action { get; init; }

    /// <summary>The type name of the affected entity (for example "ProductVariant").</summary>
    public required string EntityType { get; init; }

    /// <summary>The identifier of the affected entity instance.</summary>
    public required Guid EntityId { get; init; }

    /// <summary>The instant the mutation occurred, with offset.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>A permitted, human-readable change summary (names only, never values).</summary>
    public required string PermittedChangeSummary { get; init; }
}
