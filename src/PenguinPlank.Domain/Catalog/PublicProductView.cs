namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// The public-safe projection of a <see cref="Product"/>: the explicit allowlist of
/// Public_Ready_Field values an anonymous or customer-facing catalog consumer may see.
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>distinct public shape</b>, never a reshaped <see cref="Product"/> entity. It
/// declares <em>only</em> allowlisted public-ready members (name, public description, category,
/// and approved media) and has <b>no internal member at all</b> — no internal notes, cost,
/// margin, production note, contact information, wholesale note, publication state, or
/// concurrency token. A public response built from this shape is therefore structurally
/// incapable of carrying an Internal_Field value, which is the stronger guarantee the design's
/// Property 9 asserts (requirements R01 1.11, R10 2.10, 2.11). Hiding a field at serialization
/// time is never the sole guard: the field is absent from the type.
/// </para>
/// <para>
/// A <see cref="PublicProductView"/> is only ever produced by
/// <see cref="PublicProjectionPolicy.ToPublicView"/>, and only for a product in
/// <see cref="PublicationState.PublicApproved"/>; a <see cref="PublicationState.Draft"/> product
/// yields no view at all (the policy returns <see langword="null"/>), so draft content never
/// appears in a public projection even in part.
/// </para>
/// </remarks>
public sealed record PublicProductView
{
    /// <summary>Creates a public-safe product view.</summary>
    /// <param name="name">The public product family name.</param>
    /// <param name="category">The optional public category grouping.</param>
    /// <param name="publicDescription">The optional public-safe description.</param>
    /// <param name="media">The approved, public-safe media views. Never <see langword="null"/>.</param>
    public PublicProductView(
        string name,
        string? category,
        string? publicDescription,
        IReadOnlyList<PublicMediaView> media)
    {
        Name = name;
        Category = category;
        PublicDescription = publicDescription;
        Media = media;
    }

    /// <summary>The public product family name.</summary>
    public string Name { get; }

    /// <summary>The optional public category grouping.</summary>
    public string? Category { get; }

    /// <summary>The optional public-safe description (sourced from the product's public field only).</summary>
    public string? PublicDescription { get; }

    /// <summary>The approved, public-safe media associated with the product.</summary>
    public IReadOnlyList<PublicMediaView> Media { get; }
}
