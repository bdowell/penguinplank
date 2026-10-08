using PenguinPlank.Application.Common;

namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// A filter over the audit trail paired with the requested page (requirement A2 §5.12; A4 §6.1,
/// §6.7).
/// </summary>
/// <remarks>
/// An immutable query carried from the Owner-only <c>/audit</c> endpoint into the list use case.
/// The optional filters narrow the trail to a specific actor or affected entity; the
/// <see cref="Page"/> captures the caller's paging intent and is normalized by the reader against
/// the Phase A paging policy (requirement A4 §6.1). Results are returned most-recent-first by
/// timestamp so the newest activity surfaces at the top.
/// </remarks>
public sealed record AuditQuery
{
    /// <summary>Creates the query.</summary>
    /// <param name="page">The requested page (clamped by the reader before use).</param>
    /// <param name="actorId">An optional actor to filter by.</param>
    /// <param name="entityType">An optional affected-entity type name to filter by.</param>
    /// <param name="entityId">An optional affected-entity id to filter by.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> is <see langword="null"/>.</exception>
    public AuditQuery(
        PageRequest page,
        Guid? actorId = null,
        string? entityType = null,
        Guid? entityId = null)
    {
        ArgumentNullException.ThrowIfNull(page);

        Page = page;
        ActorId = actorId;
        EntityType = entityType;
        EntityId = entityId;
    }

    /// <summary>The requested page (clamped by the reader before use).</summary>
    public PageRequest Page { get; }

    /// <summary>An optional actor to filter by; <see langword="null"/> matches all actors.</summary>
    public Guid? ActorId { get; }

    /// <summary>An optional affected-entity type name to filter by; <see langword="null"/> matches all types.</summary>
    public string? EntityType { get; }

    /// <summary>An optional affected-entity id to filter by; <see langword="null"/> matches all ids.</summary>
    public Guid? EntityId { get; }
}
