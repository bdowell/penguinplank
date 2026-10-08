namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned transport shape for one entry in a catalog record's wood composition: a wood
/// species and its optional percentage (requirements R01 §1.5, R10 §2.2).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO with no server dependency (dependency rule; coding-standards §3). A
/// variant or piece may be composed of more than one species, so a composition is a list of these
/// components. The <see cref="Proportion"/>, when supplied, is a percentage within 0–100 that the
/// domain policy validates server-side; <see langword="null"/> means an unquantified presence.
/// </remarks>
public sealed record WoodComponentContract
{
    /// <summary>The composed wood species identifier.</summary>
    public required Guid WoodSpeciesId { get; init; }

    /// <summary>The optional proportion as a percentage within 0–100; <see langword="null"/> for an unquantified presence.</summary>
    public decimal? Proportion { get; init; }
}
