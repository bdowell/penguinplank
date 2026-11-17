using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Media;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Media;

/// <summary>
/// The EF Core <see cref="IMediaStorageKeyReader"/>: projects every <c>MediaAsset</c> storage key.
/// Captures the scoped context and is registered scoped.
/// </summary>
public sealed class EfMediaStorageKeyReader : IMediaStorageKeyReader
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the reader over the scoped application database context.</summary>
    /// <param name="dbContext">The scoped EF Core context.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public EfMediaStorageKeyReader(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAllStorageKeysAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.MediaAssets
            .AsNoTracking()
            .Select(asset => asset.StorageKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
