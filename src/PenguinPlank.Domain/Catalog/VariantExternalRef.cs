using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// A typed external reference associating a <see cref="ProductVariant"/> with a platform and
/// that platform's external identifier.
/// </summary>
/// <remarks>
/// The sibling of <see cref="ProductExternalRef"/> for variant-typed references. It owns a
/// variant-typed <see cref="VariantId"/> foreign key — never a polymorphic owner reference
/// (requirement 6.13 / design invariant 8). The combination
/// (<see cref="Platform"/>, <see cref="VariantId"/>, <see cref="ExternalId"/>) is <b>unique</b>,
/// enforced by a composite unique index in the entity configuration.
/// </remarks>
public class VariantExternalRef : Entity
{
    /// <summary>The owning <see cref="ProductVariant"/>. The typed foreign key for this reference.</summary>
    public Guid VariantId { get; set; }

    /// <summary>The external platform name (for example, "Etsy"). Part of the unique triple.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>The variant's identifier on the external platform. Part of the unique triple.</summary>
    public string ExternalId { get; set; } = string.Empty;
}
