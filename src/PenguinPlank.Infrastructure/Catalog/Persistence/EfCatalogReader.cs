using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Common;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Catalog.Persistence;

/// <summary>
/// The EF Core implementation of <see cref="ICatalogReader"/> over the
/// <see cref="PenguinPlankDbContext"/>. It lists and fetches products, variants, and pieces as
/// flat Application read models using parameterized, no-tracking queries.
/// </summary>
/// <remarks>
/// <para>
/// Every query is read-only (<see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}"/>)
/// and materializes into the Application <c>*View</c> read models — no <c>IQueryable</c>,
/// <see cref="PenguinPlankDbContext"/>, or EF entity leaves the boundary (coding-standards §2, §3).
/// List operations apply the normalized <see cref="PageRequest"/> (default size 50, clamped at 200)
/// with a stable identifier-ordered sort, and return a <see cref="Page{T}"/> carrying the total
/// matching count (requirement A4 §6.1). Each read model carries the opaque ETag token encoded from
/// the aggregate's <c>rowversion</c> so a subsequent edit can present it as an <c>If-Match</c> value
/// (requirements 6.5, 6.6). A get-by-id returns <see langword="null"/> for an absent record rather
/// than throwing (coding-standards §6). The reader captures the scoped context and is registered
/// scoped.
/// </para>
/// </remarks>
public sealed class EfCatalogReader : ICatalogReader
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the reader over the scoped application database context.</summary>
    /// <param name="dbContext">The scoped EF Core context.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public EfCatalogReader(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Page<ProductView>> ListProductsAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        PageRequest page = query.Page.Normalize();

        IQueryable<Product> filtered = _dbContext.Products.AsNoTracking();

        if (!query.IncludeArchived)
        {
            filtered = filtered.Where(product => product.ActiveFlag);
        }

        if (query.PublicationState is PublicationState publicationState)
        {
            filtered = filtered.Where(product => product.PublicationState == publicationState);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            filtered = filtered.Where(product => product.Category == query.Category);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            string term = query.SearchTerm.Trim();
            filtered = filtered.Where(product =>
                product.Name.Contains(term)
                || (product.Category != null && product.Category.Contains(term)));
        }

        long totalCount = await filtered.LongCountAsync(cancellationToken).ConfigureAwait(false);

        List<Product> products = await filtered
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ProductView> items = [.. products.Select(MapProduct)];

        return Page.Create(items, totalCount, page);
    }

    /// <inheritdoc />
    public async Task<ProductView?> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        Product? product = await _dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == productId, cancellationToken)
            .ConfigureAwait(false);

        return product is null ? null : MapProduct(product);
    }

    /// <inheritdoc />
    public async Task<Page<ProductVariantView>> ListVariantsAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        PageRequest page = query.Page.Normalize();

        IQueryable<ProductVariant> filtered = _dbContext.ProductVariants.AsNoTracking();

        if (!query.IncludeArchived)
        {
            filtered = filtered.Where(variant => variant.ActiveFlag);
        }

        if (query.ProductId is Guid productId)
        {
            filtered = filtered.Where(variant => variant.ProductId == productId);
        }

        if (query.TrackingMode is TrackingMode trackingMode)
        {
            filtered = filtered.Where(variant => variant.TrackingMode == trackingMode);
        }

        if (query.PublicationState is PublicationState publicationState)
        {
            // A variant has no publication state of its own; its public exposure is governed by its
            // owning product. Filter by the product's publication state via a correlated lookup.
            filtered = filtered.Where(variant => _dbContext.Products
                .Any(product => product.Id == variant.ProductId && product.PublicationState == publicationState));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            string term = query.SearchTerm.Trim();
            filtered = filtered.Where(variant => variant.Sku.Contains(term));
        }

        long totalCount = await filtered.LongCountAsync(cancellationToken).ConfigureAwait(false);

        List<ProductVariant> variants = await filtered
            .OrderBy(variant => variant.Sku)
            .ThenBy(variant => variant.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Guid, List<WoodComponent>> compositions =
            await LoadVariantCompositionsAsync([.. variants.Select(variant => variant.Id)], cancellationToken)
                .ConfigureAwait(false);

        List<ProductVariantView> items = [.. variants.Select(variant => MapVariant(variant, compositions))];

        return Page.Create(items, totalCount, page);
    }

    /// <inheritdoc />
    public async Task<ProductVariantView?> GetVariantAsync(Guid variantId, CancellationToken cancellationToken)
    {
        ProductVariant? variant = await _dbContext.ProductVariants
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == variantId, cancellationToken)
            .ConfigureAwait(false);

        if (variant is null)
        {
            return null;
        }

        IReadOnlyDictionary<Guid, List<WoodComponent>> compositions =
            await LoadVariantCompositionsAsync([variantId], cancellationToken).ConfigureAwait(false);

        return MapVariant(variant, compositions);
    }

    /// <inheritdoc />
    public async Task<Page<ProductPieceView>> ListPiecesAsync(PieceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        PageRequest page = query.Page.Normalize();

        IQueryable<ProductPiece> filtered = _dbContext.ProductPieces.AsNoTracking();

        if (!query.IncludeArchived)
        {
            filtered = filtered.Where(piece => piece.ActiveFlag);
        }

        if (query.VariantId is Guid variantId)
        {
            filtered = filtered.Where(piece => piece.VariantId == variantId);
        }

        if (query.PublicationState is PublicationState publicationState)
        {
            filtered = filtered.Where(piece => piece.PublicationState == publicationState);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            string term = query.SearchTerm.Trim();
            filtered = filtered.Where(piece =>
                (piece.PieceCode != null && piece.PieceCode.Contains(term))
                || (piece.Story != null && piece.Story.Contains(term)));
        }

        long totalCount = await filtered.LongCountAsync(cancellationToken).ConfigureAwait(false);

        List<ProductPiece> pieces = await filtered
            .OrderBy(piece => piece.CreatedAtUtc)
            .ThenBy(piece => piece.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ProductPieceView> items = [.. pieces.Select(MapPiece)];

        return Page.Create(items, totalCount, page);
    }

    /// <inheritdoc />
    public async Task<ProductPieceView?> GetPieceAsync(Guid pieceId, CancellationToken cancellationToken)
    {
        ProductPiece? piece = await _dbContext.ProductPieces
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == pieceId, cancellationToken)
            .ConfigureAwait(false);

        return piece is null ? null : MapPiece(piece);
    }

    private async Task<IReadOnlyDictionary<Guid, List<WoodComponent>>> LoadVariantCompositionsAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken)
    {
        if (variantIds.Count == 0)
        {
            return new Dictionary<Guid, List<WoodComponent>>();
        }

        List<VariantWood> links = await _dbContext.VariantWoods
            .AsNoTracking()
            .Where(link => variantIds.Contains(link.VariantId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, List<WoodComponent>> compositions = [];

        foreach (VariantWood link in links)
        {
            if (!compositions.TryGetValue(link.VariantId, out List<WoodComponent>? components))
            {
                components = [];
                compositions[link.VariantId] = components;
            }

            components.Add(new WoodComponent(link.WoodSpeciesId, link.Proportion));
        }

        return compositions;
    }

    private static ProductView MapProduct(Product product) => new()
    {
        ProductId = product.Id,
        Name = product.Name,
        Category = product.Category,
        PublicDescription = product.PublicDescription,
        InternalNotes = product.InternalNotes,
        PublicationState = product.PublicationState,
        IsActive = product.ActiveFlag,
        ETagToken = ETag.Encode(product.RowVersion),
        CreatedAtUtc = product.CreatedAtUtc,
        UpdatedAtUtc = product.UpdatedAtUtc,
    };

    private static ProductVariantView MapVariant(
        ProductVariant variant,
        IReadOnlyDictionary<Guid, List<WoodComponent>> compositions)
    {
        IReadOnlyList<WoodComponent> composition = compositions.TryGetValue(variant.Id, out List<WoodComponent>? components)
            ? components
            : [];

        return new ProductVariantView
        {
            VariantId = variant.Id,
            ProductId = variant.ProductId,
            Sku = variant.Sku,
            Barcode = variant.Barcode,
            TrackingMode = variant.TrackingMode,
            UnitOfMeasure = variant.UnitOfMeasure,
            Dimensions = MapDimensions(variant.Length, variant.Width, variant.Thickness, variant.Diameter, variant.DimensionUnit),
            Finish = variant.Finish,
            RetailPrice = variant.RetailPrice,
            WholesalePrice = variant.WholesalePrice,
            CasePack = variant.CasePack,
            CareProfileId = variant.CareProfileId,
            WoodComposition = composition,
            IsActive = variant.ActiveFlag,
            ETagToken = ETag.Encode(variant.RowVersion),
        };
    }

    private static ProductPieceView MapPiece(ProductPiece piece) => new()
    {
        PieceId = piece.Id,
        VariantId = piece.VariantId,
        PieceCode = piece.PieceCode,
        Dimensions = MapDimensions(piece.Length, piece.Width, piece.Thickness, piece.Diameter, piece.DimensionUnit),
        Finish = piece.Finish,
        Story = piece.Story,
        Status = piece.Status,
        ProductionDate = piece.ProductionDate,
        CareProfileVersionId = piece.CareProfileVersionId,
        PublicationState = piece.PublicationState,
        IsActive = piece.ActiveFlag,
        ETagToken = ETag.Encode(piece.RowVersion),
    };

    private static Dimensions? MapDimensions(
        decimal? length,
        decimal? width,
        decimal? thickness,
        decimal? diameter,
        string? unit)
    {
        if (length is null && width is null && thickness is null && diameter is null && unit is null)
        {
            return null;
        }

        return new Dimensions
        {
            Length = length,
            Width = width,
            Thickness = thickness,
            Diameter = diameter,
            Unit = unit,
        };
    }
}
