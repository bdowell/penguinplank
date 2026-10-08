using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// A configurable reference record for a species of wood (for example, walnut, maple,
/// cherry) used in product composition.
/// </summary>
/// <remarks>
/// Wood species is seedable reference master data referenced by <see cref="VariantWood"/> and
/// <see cref="PieceWood"/> composition links. It derives from <see cref="Entity"/> (a GUID key
/// and timestamps) rather than <see cref="VersionedEntity"/>: it is low-churn reference data,
/// not an edited aggregate requiring ETag concurrency. An archived (inactive) species remains
/// referenced by existing compositions but is not offered for new ones.
/// </remarks>
public class WoodSpecies : Entity
{
    /// <summary>The species name (for example, "Walnut"). Required.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the species is active and available for new compositions.</summary>
    public bool ActiveFlag { get; set; } = true;
}
