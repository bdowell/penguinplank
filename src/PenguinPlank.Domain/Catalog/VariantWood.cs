namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// A many-to-many composition link associating one <see cref="WoodSpecies"/> with one
/// <see cref="ProductVariant"/>, with an optional proportion.
/// </summary>
/// <remarks>
/// <para>
/// A variant may be composed of more than one wood species rather than a single wood text
/// field (requirements 1.5, 2.2). This join entity has a <b>composite primary key</b>
/// (<see cref="VariantId"/>, <see cref="WoodSpeciesId"/>) and therefore does <em>not</em>
/// derive from <c>Entity</c> (it has no surrogate GUID key and no concurrency token); its key
/// and relationships are configured explicitly.
/// </para>
/// <para>
/// <see cref="Proportion"/> is optional and, when supplied, must be within 0–100 inclusive
/// (requirement 1.5 / design invariant 13). That range check is a pure domain policy added by
/// a later task; this type declares the nullable decimal shape.
/// </para>
/// </remarks>
public class VariantWood
{
    /// <summary>The composed <see cref="ProductVariant"/>. Part of the composite primary key.</summary>
    public Guid VariantId { get; set; }

    /// <summary>The <see cref="WoodSpecies"/> in the composition. Part of the composite primary key.</summary>
    public Guid WoodSpeciesId { get; set; }

    /// <summary>
    /// The optional proportion of this species in the composition, as a percentage within
    /// 0–100 when supplied (requirement 1.5). Null means an unquantified presence.
    /// </summary>
    public decimal? Proportion { get; set; }
}
