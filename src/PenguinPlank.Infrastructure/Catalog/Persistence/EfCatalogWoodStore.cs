using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Catalog.Persistence;

/// <summary>
/// The EF Core implementation of <see cref="ICatalogWoodStore"/> over the
/// <see cref="PenguinPlankDbContext"/>. It confirms the target variant or piece is active and
/// replaces its wood composition in the matching join table.
/// </summary>
/// <remarks>
/// <para>
/// The set-wood-composition use case validates every proportion with the pure
/// <c>WoodCompositionPolicy</c> and the archived guard before calling this store; the adapter holds
/// no business rule (coding-standards §1, §3). A composition is replaced atomically — the existing
/// join rows for the target are removed and the accepted components are inserted in one
/// <c>SaveChanges</c>, so a partial write never leaves a mixed composition. An empty component set
/// clears the composition. The store captures the scoped context and is registered scoped; no EF
/// entity crosses the boundary (coding-standards §2, §3).
/// </para>
/// </remarks>
public sealed class EfCatalogWoodStore : ICatalogWoodStore
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the store over the scoped application database context.</summary>
    /// <param name="dbContext">The scoped EF Core context.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public EfCatalogWoodStore(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<bool?> GetTargetActiveAsync(CatalogRef target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.Kind switch
        {
            CatalogEntityKind.Variant => await _dbContext.ProductVariants
                .AsNoTracking()
                .Where(variant => variant.Id == target.Id)
                .Select(variant => (bool?)variant.ActiveFlag)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),

            CatalogEntityKind.Piece => await _dbContext.ProductPieces
                .AsNoTracking()
                .Where(piece => piece.Id == target.Id)
                .Select(piece => (bool?)piece.ActiveFlag)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),

            _ => throw new ArgumentOutOfRangeException(
                nameof(target),
                target.Kind,
                "Wood composition applies to a variant or a piece, not a product."),
        };
    }

    /// <inheritdoc />
    public async Task ReplaceCompositionAsync(
        CatalogRef target,
        IReadOnlyList<WoodComponent> components,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(components);

        switch (target.Kind)
        {
            case CatalogEntityKind.Variant:
                await ReplaceVariantCompositionAsync(target.Id, components, cancellationToken).ConfigureAwait(false);
                break;

            case CatalogEntityKind.Piece:
                await ReplacePieceCompositionAsync(target.Id, components, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(target),
                    target.Kind,
                    "Wood composition applies to a variant or a piece, not a product.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ReplaceVariantCompositionAsync(
        Guid variantId,
        IReadOnlyList<WoodComponent> components,
        CancellationToken cancellationToken)
    {
        List<VariantWood> existing = await _dbContext.VariantWoods
            .Where(link => link.VariantId == variantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _dbContext.VariantWoods.RemoveRange(existing);

        foreach (WoodComponent component in components)
        {
            _dbContext.VariantWoods.Add(new VariantWood
            {
                VariantId = variantId,
                WoodSpeciesId = component.WoodSpeciesId,
                Proportion = component.Proportion,
            });
        }
    }

    private async Task ReplacePieceCompositionAsync(
        Guid pieceId,
        IReadOnlyList<WoodComponent> components,
        CancellationToken cancellationToken)
    {
        List<PieceWood> existing = await _dbContext.PieceWoods
            .Where(link => link.PieceId == pieceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _dbContext.PieceWoods.RemoveRange(existing);

        foreach (WoodComponent component in components)
        {
            _dbContext.PieceWoods.Add(new PieceWood
            {
                PieceId = pieceId,
                WoodSpeciesId = component.WoodSpeciesId,
                Proportion = component.Proportion,
            });
        }
    }
}
