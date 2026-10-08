using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Catalog.Persistence;

/// <summary>
/// The EF Core implementation of <see cref="ICatalogVariantStore"/> over the
/// <see cref="PenguinPlankDbContext"/>. It loads the existing-state facts a variant use case
/// decides against and persists the decided outcome, running parameterized queries only.
/// </summary>
/// <remarks>
/// <para>
/// This adapter holds no business rule: it reads the SKUs/barcodes in scope, a variant's
/// active/tracking/stock-history facts, and a product's active flag as ordinary values, and it
/// inserts a decided <see cref="NewVariantRecord"/> or flips an active flag exactly as the use case
/// asked (coding-standards §1, §3). No <c>IQueryable</c>, <see cref="PenguinPlankDbContext"/>, or EF
/// entity leaves the boundary — every method materializes to the Application snapshot/record types
/// or primitives (coding-standards §2, §3). It captures the scoped context, so it is registered
/// scoped (coding-standards §2). Stock history does not exist in Phase A, so
/// <see cref="VariantFacts.HasStockHistory"/> is reported as <see langword="false"/>; the later
/// phase that adds inventory movement sets it from the real ledger.
/// </para>
/// </remarks>
public sealed class EfCatalogVariantStore : ICatalogVariantStore
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the store over the scoped application database context.</summary>
    /// <param name="dbContext">The scoped EF Core context.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public EfCatalogVariantStore(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<CatalogUniquenessSnapshot> GetUniquenessSnapshotAsync(
        VariantUniquenessQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        Guid? excludeVariantId = query.ExcludeVariantId;

        List<string> skus = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => excludeVariantId == null || variant.Id != excludeVariantId)
            .Select(variant => variant.Sku)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<string> barcodes = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => (excludeVariantId == null || variant.Id != excludeVariantId)
                && variant.Barcode != null)
            .Select(variant => variant.Barcode!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new CatalogUniquenessSnapshot
        {
            ExistingSkus = skus,
            ExistingBarcodes = barcodes,
        };
    }

    /// <inheritdoc />
    public async Task<VariantFacts?> GetVariantFactsAsync(Guid variantId, CancellationToken cancellationToken)
    {
        VariantFacts? facts = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.Id == variantId)
            .Select(variant => new VariantFacts
            {
                VariantId = variant.Id,
                ProductId = variant.ProductId,
                IsActive = variant.ActiveFlag,
                TrackingMode = variant.TrackingMode,

                // Stock history is a later-phase concern (inventory movements). No ledger exists
                // in Phase A, so no variant yet has recorded stock history.
                HasStockHistory = false,
                RowVersion = variant.RowVersion,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return facts;
    }

    /// <inheritdoc />
    public async Task<bool?> GetProductActiveAsync(Guid productId, CancellationToken cancellationToken)
    {
        var row = await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == productId)
            .Select(product => new { product.ActiveFlag })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row?.ActiveFlag;
    }

    /// <inheritdoc />
    public async Task InsertVariantAsync(NewVariantRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var variant = new ProductVariant
        {
            Id = record.VariantId,
            ProductId = record.ProductId,
            Sku = record.Sku,
            Barcode = record.Barcode,
            TrackingMode = record.TrackingMode,
            UnitOfMeasure = record.UnitOfMeasure,
            Length = record.Dimensions?.Length,
            Width = record.Dimensions?.Width,
            Thickness = record.Dimensions?.Thickness,
            Diameter = record.Dimensions?.Diameter,
            DimensionUnit = record.Dimensions?.Unit,
            Finish = record.Finish,
            RetailPrice = record.RetailPrice,
            WholesalePrice = record.WholesalePrice,
            CasePack = record.CasePack,
            CareProfileId = record.CareProfileId,

            // A ProductVariant has no publication state of its own: a variant's public exposure is
            // governed by its owning product (only Product and ProductPiece carry a publication
            // state). NewVariantRecord.PublicationState documents the always-Draft decision at the
            // use-case layer but has no variant column to persist to, so it is intentionally not
            // mapped here.
            ActiveFlag = true,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.CreatedAtUtc,
        };

        _dbContext.ProductVariants.Add(variant);

        foreach (WoodComponent component in record.WoodComposition)
        {
            _dbContext.VariantWoods.Add(new VariantWood
            {
                VariantId = record.VariantId,
                WoodSpeciesId = component.WoodSpeciesId,
                Proportion = component.Proportion,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetVariantActiveAsync(
        Guid variantId,
        bool isActive,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        ProductVariant? variant = await _dbContext.ProductVariants
            .SingleOrDefaultAsync(candidate => candidate.Id == variantId, cancellationToken)
            .ConfigureAwait(false);

        if (variant is null)
        {
            // The use case loaded the variant's facts before calling this; a missing row now is a
            // broken protocol, not an expected business outcome (coding-standards §6).
            throw new InvalidOperationException(
                $"No ProductVariant exists for id '{variantId}' to change its active flag.");
        }

        variant.ActiveFlag = isActive;
        variant.UpdatedAtUtc = updatedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
