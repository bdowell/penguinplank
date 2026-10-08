using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Catalog.Persistence;

/// <summary>
/// The EF Core implementation of <see cref="ICatalogPieceStore"/> over the
/// <see cref="PenguinPlankDbContext"/>. It loads the owning variant's facts, its care-profile
/// versions, and the catalog's existing piece codes, then inserts a decided piece.
/// </summary>
/// <remarks>
/// <para>
/// The adapter makes no business decision: the create-piece use case applies the archived guard,
/// the piece-code uniqueness check, and the care-version resolution over the facts this store
/// returns (coding-standards §1, §3). The care-profile versions are returned as pure Domain
/// <see cref="CareProfileVersion"/> entities (a Domain type, not a persistence type), so no EF
/// persistence type crosses the boundary (coding-standards §2, §3). The store captures the scoped
/// context and is registered scoped.
/// </para>
/// </remarks>
public sealed class EfCatalogPieceStore : ICatalogPieceStore
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the store over the scoped application database context.</summary>
    /// <param name="dbContext">The scoped EF Core context.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public EfCatalogPieceStore(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<PieceCreationFacts?> GetPieceCreationFactsAsync(Guid variantId, CancellationToken cancellationToken)
    {
        var variant = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(candidate => candidate.Id == variantId)
            .Select(candidate => new
            {
                candidate.ActiveFlag,
                candidate.TrackingMode,
                candidate.CareProfileId,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (variant is null)
        {
            return null;
        }

        IReadOnlyList<CareProfileVersion> careProfileVersions = variant.CareProfileId is Guid careProfileId
            ? await _dbContext.CareProfileVersions
                .AsNoTracking()
                .Where(version => version.CareProfileId == careProfileId)
                .OrderBy(version => version.VersionNumber)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        List<string?> existingPieceCodes = await _dbContext.ProductPieces
            .AsNoTracking()
            .Where(piece => piece.PieceCode != null)
            .Select(piece => piece.PieceCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PieceCreationFacts
        {
            VariantIsActive = variant.ActiveFlag,
            VariantTrackingMode = variant.TrackingMode,
            CareProfileId = variant.CareProfileId,
            CareProfileVersions = careProfileVersions,
            ExistingPieceCodes = existingPieceCodes,
        };
    }

    /// <inheritdoc />
    public async Task InsertPieceAsync(NewPieceRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var piece = new ProductPiece
        {
            Id = record.PieceId,
            VariantId = record.VariantId,
            PieceCode = record.PieceCode,
            Length = record.Dimensions?.Length,
            Width = record.Dimensions?.Width,
            Thickness = record.Dimensions?.Thickness,
            Diameter = record.Dimensions?.Diameter,
            DimensionUnit = record.Dimensions?.Unit,
            Finish = record.Finish,
            Story = record.Story,
            Status = record.Status,
            ProductionDate = record.ProductionDate,
            CareProfileVersionId = record.CareProfileVersionId,
            PublicationState = record.PublicationState,
            ActiveFlag = true,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.CreatedAtUtc,
        };

        _dbContext.ProductPieces.Add(piece);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
