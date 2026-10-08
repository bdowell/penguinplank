using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// An immutable version of care guidance owned by a <see cref="CareProfile"/>. Editing a
/// profile's guidance appends a new version and never modifies an existing one.
/// </summary>
/// <remarks>
/// <para>
/// A care-profile version is <b>append-only and immutable once created</b> (requirements 2.3,
/// 2.5 / design invariant 10): it is inserted exactly once and never updated or deleted. For
/// that reason it derives from <see cref="Entity"/> rather than <see cref="VersionedEntity"/> —
/// an immutable record needs no <c>rowversion</c> optimistic-concurrency token because nothing
/// ever edits it. <see cref="Entity.CreatedAtUtc"/> records when the version was created; the
/// inherited <c>UpdatedAtUtc</c> is not meaningfully used for this record and is left at its
/// creation value.
/// </para>
/// <para>
/// A produced <see cref="ProductPiece"/> references the specific version in effect at its
/// production time and keeps resolving that exact version after later edits (requirement 2.4).
/// Immutability is reinforced at the persistence layer: the entity configuration maps this as
/// an insert-only record and does not expose an update path.
/// </para>
/// </remarks>
public class CareProfileVersion : Entity
{
    /// <summary>The owning <see cref="CareProfile"/>. Required.</summary>
    public Guid CareProfileId { get; set; }

    /// <summary>
    /// The monotonically increasing version number within the owning profile. The first
    /// version is 1; each care edit appends the next number.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>The care guidance text captured by this immutable version. Required.</summary>
    public string Guidance { get; set; } = string.Empty;
}
