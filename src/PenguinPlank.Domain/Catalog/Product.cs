using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// A design or product family record (for example, "end-grain cutting board"). A product
/// owns one or more <see cref="ProductVariant"/> sellable SKUs.
/// </summary>
/// <remarks>
/// <para>
/// A product is a <b>mutable aggregate</b> (its name, description, and notes are edited over
/// its life), so it derives from <see cref="VersionedEntity"/> and carries a
/// <c>rowversion</c> concurrency token surfaced as an HTTP ETag.
/// </para>
/// <para>
/// Public-ready and internal content are stored in <b>separate columns</b> so neither
/// overwrites the other (requirement 1.11 / design invariant 11): <see cref="PublicDescription"/>
/// is eligible for public exposure while <see cref="InternalNotes"/> must never leave the
/// authenticated staff surface. A product's public content starts in
/// <see cref="PublicationState.Draft"/> (requirement 1.12).
/// </para>
/// <para>
/// This is a schema-focused POCO: it declares the shape and the Draft default. Business rules
/// (publication transitions, archived-record transaction guards, public projection) live in
/// pure domain policies added by later tasks (7.x), not here.
/// </para>
/// </remarks>
public class Product : VersionedEntity
{
    /// <summary>The product family name. Required; a public-ready field.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The product category grouping (for example, "Cutting boards").</summary>
    public string? Category { get; set; }

    /// <summary>
    /// The public-safe product description. A <b>public-ready field</b> stored separately
    /// from <see cref="InternalNotes"/> so a public projection can expose it without ever
    /// touching internal content.
    /// </summary>
    public string? PublicDescription { get; set; }

    /// <summary>
    /// Internal-only notes about the product. An <b>internal field</b> that must never be
    /// exposed publicly or to unauthorized roles; stored separately from
    /// <see cref="PublicDescription"/>.
    /// </summary>
    public string? InternalNotes { get; set; }

    /// <summary>
    /// The publication lifecycle state. Defaults to <see cref="PublicationState.Draft"/> for
    /// newly created content (requirement 1.12); the same default is enforced at the database
    /// level in the entity configuration.
    /// </summary>
    public PublicationState PublicationState { get; set; } = PublicationState.Draft;

    /// <summary>
    /// Whether the product is active. An archived (inactive) product remains readable but
    /// rejects new transactions (requirement 1.8); the rejection rule is a domain policy, not
    /// enforced by this flag alone.
    /// </summary>
    public bool ActiveFlag { get; set; } = true;
}
