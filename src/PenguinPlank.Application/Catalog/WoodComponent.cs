namespace PenguinPlank.Application.Catalog;

/// <summary>
/// One entry in a wood composition: a <see cref="WoodSpeciesId"/> and its optional
/// <see cref="Proportion"/> percentage.
/// </summary>
/// <remarks>
/// A variant or piece may be composed of more than one wood species rather than a single wood
/// text field (requirements 1.5, 2.2). A composition request carries a collection of these
/// components; <see cref="Proportion"/>, when supplied, is validated against the 0–100 range by
/// the wood-composition domain policy inside the use case. This is an immutable input value with
/// no I/O (coding-standards §1).
/// </remarks>
public sealed record WoodComponent
{
    /// <summary>Creates a wood composition component.</summary>
    /// <param name="woodSpeciesId">The composed wood species; must be non-empty.</param>
    /// <param name="proportion">The optional percentage within 0–100, or <see langword="null"/> for an unquantified presence.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="woodSpeciesId"/> is empty.</exception>
    public WoodComponent(Guid woodSpeciesId, decimal? proportion = null)
    {
        if (woodSpeciesId == Guid.Empty)
        {
            throw new ArgumentException("A wood component requires a non-empty species id.", nameof(woodSpeciesId));
        }

        WoodSpeciesId = woodSpeciesId;
        Proportion = proportion;
    }

    /// <summary>The composed wood species.</summary>
    public Guid WoodSpeciesId { get; }

    /// <summary>
    /// The optional proportion of this species as a percentage within 0–100 when supplied
    /// (requirement 1.5). Null means an unquantified presence.
    /// </summary>
    public decimal? Proportion { get; }
}
