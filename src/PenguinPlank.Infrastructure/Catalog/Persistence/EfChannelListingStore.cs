using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Catalog.Persistence;

/// <summary>
/// The EF Core implementation of <see cref="IChannelListingStore"/> over the
/// <see cref="PenguinPlankDbContext"/>. It confirms the listed variant is active and inserts the
/// listing reference.
/// </summary>
/// <remarks>
/// <para>
/// A channel listing is a <b>reference record only</b> in Phase A — the record-channel-listing use
/// case validates the required fields and the archived guard, and this store performs no channel
/// synchronization (requirement 1.10). The adapter holds no business rule and leaks no EF type
/// across the boundary (coding-standards §1, §2, §3). It captures the scoped context and is
/// registered scoped.
/// </para>
/// </remarks>
public sealed class EfChannelListingStore : IChannelListingStore
{
    private readonly PenguinPlankDbContext _dbContext;

    /// <summary>Creates the store over the scoped application database context.</summary>
    /// <param name="dbContext">The scoped EF Core context.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public EfChannelListingStore(PenguinPlankDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<bool?> GetVariantActiveAsync(Guid variantId, CancellationToken cancellationToken)
    {
        return await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.Id == variantId)
            .Select(variant => (bool?)variant.ActiveFlag)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task InsertListingAsync(NewChannelListingRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var listing = new ChannelListing
        {
            Id = record.ChannelListingId,
            VariantId = record.VariantId,
            Channel = record.Channel,
            ExternalId = record.ExternalId,
            Status = record.Status,
            Url = record.Url,
            ReadinessState = record.ReadinessState,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.CreatedAtUtc,
        };

        _dbContext.ChannelListings.Add(listing);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
