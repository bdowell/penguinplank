namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The narrow persistence boundary a record-channel-listing use case depends on to <b>confirm the
/// listed variant is active</b> and to <b>persist the listing reference</b>.
/// </summary>
/// <remarks>
/// <para>
/// This interface exists so the record-channel-listing use case (task 8.2) can be orchestrated and
/// unit-tested against a controllable substitute (task 8.4) while Infrastructure provides the EF
/// Core implementation (task 8.3). It is a focused, responsibility-named boundary (coding-standards
/// §2, §6). A channel listing is a reference record only; Phase A performs no synchronization for
/// it (requirement 1.10). Every method takes and propagates a <see cref="CancellationToken"/>; the
/// active-state read returns <see langword="null"/> for an absent variant. No EF entity crosses the
/// boundary (coding-standards §2, §3).
/// </para>
/// </remarks>
public interface IChannelListingStore
{
    /// <summary>
    /// Determines whether the listed variant exists and is active, or <see langword="null"/> when
    /// no variant matches.
    /// </summary>
    /// <param name="variantId">The variant the listing references.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> when the variant is active, <see langword="false"/> when archived, or
    /// <see langword="null"/> when no variant matches.
    /// </returns>
    Task<bool?> GetVariantActiveAsync(Guid variantId, CancellationToken cancellationToken);

    /// <summary>Inserts an accepted new channel-listing reference exactly as the use case decided it.</summary>
    /// <param name="record">The fully decided listing values to persist.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the listing has been persisted.</returns>
    Task InsertListingAsync(NewChannelListingRecord record, CancellationToken cancellationToken);
}
