namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The optional physical sizing supplied for a variant or piece: length, width, thickness, or a
/// round-product diameter, together with the explicit <see cref="Unit"/> they are measured in.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Dimensions"/> is an immutable input value carried across the catalog write
/// boundary. It mirrors the domain's dimension shape (every supplied value is a non-negative
/// decimal with an explicit unit; a round product may supply <see cref="Diameter"/> in place of
/// <see cref="Length"/>/<see cref="Width"/>, requirements 1.3, 1.4). It carries the caller's
/// intent only — the <c>DimensionPolicy</c> domain rule validates it inside the use case; this
/// record performs no I/O and is directly testable (coding-standards §1).
/// </para>
/// </remarks>
public sealed record Dimensions
{
    /// <summary>The length, when supplied. Non-negative with an explicit <see cref="Unit"/>.</summary>
    public decimal? Length { get; init; }

    /// <summary>The width, when supplied. Non-negative with an explicit <see cref="Unit"/>.</summary>
    public decimal? Width { get; init; }

    /// <summary>The thickness or height, when supplied. Non-negative with an explicit <see cref="Unit"/>.</summary>
    public decimal? Thickness { get; init; }

    /// <summary>The diameter for a round product, supplied in place of length/width (requirement 1.4).</summary>
    public decimal? Diameter { get; init; }

    /// <summary>
    /// The explicit unit for the supplied values (for example, "in" or "cm"). Required by the
    /// domain policy whenever any dimension is supplied (requirement 1.3).
    /// </summary>
    public string? Unit { get; init; }
}
