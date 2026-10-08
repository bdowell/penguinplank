using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// A record associating an external platform resource (product, variant, order, line, or
/// location) with an internal record, preserving the platform, account, and external identifiers
/// under a unique association.
/// </summary>
/// <remarks>
/// <para>
/// A mapping preserves the <see cref="Platform"/>, <see cref="Account"/>, and
/// <see cref="ExternalId"/> of the external resource and links it to one internal record
/// (requirement 3.4 / R11). The combination
/// (<see cref="Platform"/>, <see cref="Account"/>, <see cref="ExternalId"/>) is <b>unique</b>,
/// enforced by a composite unique index, so one external resource cannot be mapped to two
/// internal records and a duplicate triple is rejected.
/// </para>
/// <para>
/// The internal side uses a <b>typed owner reference</b> — <see cref="OwnerType"/> names the
/// mapped internal entity kind and <see cref="InternalId"/> its id — scoped by the owning
/// <see cref="ConnectionId"/>. A detected SKU match is surfaced as a reviewable suggestion by a
/// later task, never written here as a silent authoritative mapping (requirement 3.5).
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: a mapping is created once per external resource and is a
/// lightweight association record rather than a concurrency-protected aggregate.
/// </para>
/// </remarks>
public class EntityMapping : Entity
{
    /// <summary>The owning <see cref="IntegrationConnection"/>.</summary>
    public Guid ConnectionId { get; set; }

    /// <summary>The platform the external resource belongs to. Part of the unique triple.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>The account or shop the external resource belongs to. Part of the unique triple.</summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>The external resource's identifier on the platform. Part of the unique triple.</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>
    /// The kind of internal entity this maps to (for example, "ProductVariant", "ExternalOrder").
    /// A typed owner discriminator — not an unchecked polymorphic link.
    /// </summary>
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>The identifier of the internal record the external resource maps to.</summary>
    public Guid InternalId { get; set; }
}
