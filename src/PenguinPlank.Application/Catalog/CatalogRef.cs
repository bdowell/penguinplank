namespace PenguinPlank.Application.Catalog;

/// <summary>
/// The kind of catalog master-data record a <see cref="CatalogRef"/> points at.
/// </summary>
/// <remarks>
/// A single <see cref="CatalogRef"/> can target any of the three archivable catalog
/// aggregates, so operations such as archive/unarchive are expressed once over a
/// <see cref="CatalogRef"/> rather than duplicated per entity (coding-standards §5).
/// </remarks>
public enum CatalogEntityKind
{
    /// <summary>A <c>Product</c> family record.</summary>
    Product = 0,

    /// <summary>A sellable <c>ProductVariant</c> (SKU).</summary>
    Variant = 1,

    /// <summary>An individual serialized <c>ProductPiece</c>.</summary>
    Piece = 2,
}

/// <summary>
/// A typed reference to one catalog master-data record: its <see cref="Kind"/> and stable
/// identifier.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CatalogRef"/> is an immutable value object carried across the Application
/// boundary so that write operations which apply uniformly to any catalog aggregate — most
/// notably archive and unarchive — take a single explicit target instead of leaking an EF
/// entity or forcing a separate method per entity (coding-standards §2, §5). It holds ordinary
/// data, performs no I/O, and is directly testable (coding-standards §1).
/// </para>
/// </remarks>
public sealed record CatalogRef
{
    /// <summary>Creates a reference to a catalog record.</summary>
    /// <param name="kind">The kind of record referenced.</param>
    /// <param name="id">The stable identifier of the record; must be non-empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is empty.</exception>
    public CatalogRef(CatalogEntityKind kind, Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A catalog reference requires a non-empty id.", nameof(id));
        }

        Kind = kind;
        Id = id;
    }

    /// <summary>The kind of catalog record referenced.</summary>
    public CatalogEntityKind Kind { get; }

    /// <summary>The stable identifier of the referenced record.</summary>
    public Guid Id { get; }

    /// <summary>Creates a reference to a <c>Product</c>.</summary>
    /// <param name="id">The product identifier; must be non-empty.</param>
    public static CatalogRef Product(Guid id) => new(CatalogEntityKind.Product, id);

    /// <summary>Creates a reference to a <c>ProductVariant</c>.</summary>
    /// <param name="id">The variant identifier; must be non-empty.</param>
    public static CatalogRef Variant(Guid id) => new(CatalogEntityKind.Variant, id);

    /// <summary>Creates a reference to a <c>ProductPiece</c>.</summary>
    /// <param name="id">The piece identifier; must be non-empty.</param>
    public static CatalogRef Piece(Guid id) => new(CatalogEntityKind.Piece, id);
}
