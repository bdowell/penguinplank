namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// A many-to-many composition link associating one <see cref="WoodSpecies"/> with one
/// <see cref="ProductPiece"/>, with an optional proportion.
/// </summary>
/// <remarks>
/// <para>
/// An individual piece may be composed of more than one wood species rather than a single
/// wood text field (requirements 1.5, 2.2). Like <see cref="VariantWood"/>, this join entity
/// has a <b>composite primary key</b> (<see cref="PieceId"/>, <see cref="WoodSpeciesId"/>) and
/// does <em>not</em> derive from <c>Entity</c>; its key and relationships are configured
/// explicitly.
/// </para>
/// <para>
/// <see cref="Proportion"/> is optional and, when supplied, must be within 0–100 inclusive
/// (requirement 1.5 / design invariant 13) — enforced by a pure domain policy, not here.
/// </para>
/// </remarks>
public class PieceWood
{
    /// <summary>The composed <see cref="ProductPiece"/>. Part of the composite primary key.</summary>
    public Guid PieceId { get; set; }

    /// <summary>The <see cref="WoodSpecies"/> in the composition. Part of the composite primary key.</summary>
    public Guid WoodSpeciesId { get; set; }

    /// <summary>
    /// The optional proportion of this species in the composition, as a percentage within
    /// 0–100 when supplied (requirement 1.5). Null means an unquantified presence.
    /// </summary>
    public decimal? Proportion { get; set; }
}
