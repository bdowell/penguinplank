using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Media;
using PenguinPlank.Domain.Media;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Media;

/// <summary>
/// The EF Core implementation of <see cref="IMediaAssetStore"/> over the
/// <see cref="PenguinPlankDbContext"/>. It inserts a decided <c>MediaAsset</c> metadata row, reads
/// a single asset's metadata, and applies an editable-metadata update, mapping to the Application
/// <see cref="MediaAssetView"/> read model (requirements 6.8–6.11 / A4).
/// </summary>
/// <remarks>
/// <para>
/// The adapter makes no business or authorization decision: the media use cases decide, and this
/// store only reads and writes rows (coding-standards §1, §3). It projects to the Application
/// <see cref="MediaAssetView"/> and <b>never</b> returns an EF entity, <c>DbContext</c>, or
/// <c>IQueryable</c> across the boundary. The binary content lives in the file store outside the
/// database and web root; this store holds only metadata (requirement 6.8). It captures the scoped
/// context and is registered scoped.
/// </para>
/// </remarks>
public sealed class EfMediaAssetStore : IMediaAssetStore
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the store over the scoped application database context.</summary>
    /// <param name="dbContext">The scoped EF Core context.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public EfMediaAssetStore(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task InsertAsync(NewMediaAssetRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var asset = new MediaAsset
        {
            Id = record.MediaAssetId,
            StorageKey = record.StorageKey,
            MimeType = record.MimeType,
            SizeBytes = record.SizeBytes,
            Checksum = record.Checksum,
            Caption = record.Caption,
            Role = null,
            SortOrder = 0,
            Visibility = record.Visibility,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.CreatedAtUtc,
        };

        _dbContext.MediaAssets.Add(asset);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MediaAssetView?> GetAsync(Guid mediaAssetId, CancellationToken cancellationToken)
    {
        return await _dbContext.MediaAssets
            .AsNoTracking()
            .Where(asset => asset.Id == mediaAssetId)
            .Select(asset => ToView(asset))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MediaAssetView?> UpdateMetadataAsync(
        UpdateMediaMetadataRequest request,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        MediaAsset? asset = await _dbContext.MediaAssets
            .SingleOrDefaultAsync(candidate => candidate.Id == request.MediaAssetId, cancellationToken)
            .ConfigureAwait(false);

        if (asset is null)
        {
            return null;
        }

        asset.Caption = request.Caption;
        asset.Role = request.Role;
        asset.SortOrder = request.SortOrder;
        asset.Visibility = request.Visibility;
        asset.UpdatedAtUtc = updatedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToView(asset);
    }

    private static MediaAssetView ToView(MediaAsset asset) => new()
    {
        MediaAssetId = asset.Id,
        StorageKey = asset.StorageKey,
        MimeType = asset.MimeType,
        SizeBytes = asset.SizeBytes,
        Checksum = asset.Checksum,
        Caption = asset.Caption,
        Role = asset.Role,
        SortOrder = asset.SortOrder,
        Visibility = asset.Visibility,
        CreatedAtUtc = asset.CreatedAtUtc,
        UpdatedAtUtc = asset.UpdatedAtUtc,
    };
}
