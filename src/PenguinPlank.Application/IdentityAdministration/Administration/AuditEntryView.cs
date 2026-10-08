namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// The Application read model for a single audit-trail entry (requirement A2 §5.12; A4 §6.7).
/// </summary>
/// <remarks>
/// A persistence-agnostic view mapped from the <c>AuditEntry</c> entity; no EF entity,
/// <c>DbContext</c>, or <c>IQueryable</c> crosses the boundary (coding-standards §3). It carries
/// only the permitted, non-sensitive summary — never property values, secrets, or unnecessary
/// personal data (requirement A4 §6.7). The audit trail is sensitive, so the <c>/audit</c> endpoint
/// that returns these views is Owner-only.
/// </remarks>
public sealed record AuditEntryView
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

    /// <summary>A permitted, human-readable summary of what changed (names only, never values).</summary>
    public required string PermittedChangeSummary { get; init; }
}
