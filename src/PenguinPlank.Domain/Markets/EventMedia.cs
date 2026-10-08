namespace PenguinPlank.Domain.Markets;

/// <summary>
/// An explicit foreign-key join associating a Media <c>MediaAsset</c> with an
/// <see cref="Event"/>, with the asset's sort order within that event's media.
/// </summary>
/// <remarks>
/// <para>
/// Designed-only in Phase A (design Section 6). Like the Catalog media joins, event media uses
/// an <b>explicit foreign-key join table per owner type</b> — never a polymorphic
/// <c>EntityType</c>/<c>EntityId</c> link (design invariant 9). It carries an
/// <see cref="EventId"/> and a <see cref="MediaAssetId"/>.
/// </para>
/// <para>
/// It has a <b>composite primary key</b> (<see cref="EventId"/>, <see cref="MediaAssetId"/>)
/// and does <em>not</em> derive from <c>Entity</c>; its key and both foreign keys are
/// configured in Infrastructure. The FK to <see cref="Event"/> is real (both tables exist in
/// Phase A); the FK to <c>MediaAsset</c> is likewise real.
/// </para>
/// </remarks>
public class EventMedia
{
    /// <summary>The owning <see cref="Event"/>. The typed foreign key and part of the composite key.</summary>
    public Guid EventId { get; set; }

    /// <summary>The linked Media <c>MediaAsset</c>. Part of the composite key.</summary>
    public Guid MediaAssetId { get; set; }

    /// <summary>The sort order of the asset within this event's media.</summary>
    public int SortOrder { get; set; }
}
