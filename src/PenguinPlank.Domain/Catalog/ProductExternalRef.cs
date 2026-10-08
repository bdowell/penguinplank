using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// A typed external reference associating a <see cref="Product"/> with a platform and that
/// platform's external identifier.
/// </summary>
/// <remarks>
/// <para>
/// External references use a <b>typed foreign key per owner type</b> — never an unchecked
/// polymorphic <c>EntityType</c>/<c>EntityId</c> pair (requirement 6.13 / design invariant 8).
/// This type owns a product-typed <see cref="ProductId"/> foreign key; variant references use
/// the sibling <see cref="VariantExternalRef"/>. The combination
/// (<see cref="Platform"/>, <see cref="ProductId"/>, <see cref="ExternalId"/>) is <b>unique</b>
/// so one product cannot carry two conflicting references to the same external resource on the
/// same platform; the uniqueness is enforced by a composite unique index in the entity
/// configuration.
/// </para>
/// </remarks>
public class ProductExternalRef : Entity
{
    /// <summary>The owning <see cref="Product"/>. The typed foreign key for this reference.</summary>
    public Guid ProductId { get; set; }

    /// <summary>The external platform name (for example, "Shopify"). Part of the unique triple.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>The product's identifier on the external platform. Part of the unique triple.</summary>
    public string ExternalId { get; set; } = string.Empty;
}
