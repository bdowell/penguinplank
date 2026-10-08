namespace PenguinPlank.Domain.Common;

/// <summary>
/// The base type for persisted domain entities. It carries only the fields every
/// entity shares: a GUID identity and creation/modification instants.
/// </summary>
/// <remarks>
/// <para>
/// This is a plain domain POCO. The Domain layer takes <b>no</b> dependency on EF Core,
/// so this type carries no persistence attributes, no <c>[Key]</c>, no <c>[Timestamp]</c>,
/// and no navigation-tracking concerns (coding-standards §3; design "Global column and
/// type conventions"). The physical mapping — <c>uniqueidentifier</c> primary key,
/// <c>datetimeoffset</c> for the timestamps — lives in Infrastructure through the base
/// entity convention applied in the <c>PenguinPlankDbContext</c>.
/// </para>
/// <para>
/// <see cref="Id"/> is a GUID primary key. Unique business codes (SKU, piece code,
/// barcode) are modeled as separate indexed columns on the concrete entities, never as
/// the key.
/// </para>
/// <para>
/// Immutable aggregates (append-only records such as a care-profile version or an audit
/// entry) derive from <see cref="Entity"/> directly. Mutable aggregates that need
/// optimistic-concurrency protection derive from <see cref="VersionedEntity"/>, which adds
/// a <c>rowversion</c> concurrency token.
/// </para>
/// </remarks>
public abstract class Entity
{
    /// <summary>
    /// The GUID primary key. Mapped to a SQL <c>uniqueidentifier</c> by the base entity
    /// convention in Infrastructure.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The instant the entity was first persisted, as a UTC
    /// <see cref="DateTimeOffset"/>. Mapped to <c>datetimeoffset</c>.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// The instant the entity was last modified, as a UTC
    /// <see cref="DateTimeOffset"/>. Mapped to <c>datetimeoffset</c>.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
