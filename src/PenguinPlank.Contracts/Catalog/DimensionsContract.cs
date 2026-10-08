namespace PenguinPlank.Contracts.Catalog;

/// <summary>
/// The versioned transport shape for a catalog record's physical sizing: length, width,
/// thickness, or a round-product diameter, together with the explicit <see cref="Unit"/> they are
/// measured in (requirements R01 §1.3, §1.4).
/// </summary>
/// <remarks>
/// A standalone Contracts DTO with no server dependency (dependency rule; coding-standards §3).
/// Every measurement is a <see cref="decimal"/> and the unit travels with the values, so a client
/// never has to assume a unit. The API maps this to and from the Application layer's dimension
/// value; the domain dimension policy validates it server-side.
/// </remarks>
public sealed record DimensionsContract
{
    /// <summary>The length, when supplied, in <see cref="Unit"/>.</summary>
    public decimal? Length { get; init; }

    /// <summary>The width, when supplied, in <see cref="Unit"/>.</summary>
    public decimal? Width { get; init; }

    /// <summary>The thickness or height, when supplied, in <see cref="Unit"/>.</summary>
    public decimal? Thickness { get; init; }

    /// <summary>The diameter for a round product, supplied in place of length/width (requirement 1.4).</summary>
    public decimal? Diameter { get; init; }

    /// <summary>The explicit unit for the supplied values (for example "in" or "cm"). Required when any value is supplied.</summary>
    public string? Unit { get; init; }
}
