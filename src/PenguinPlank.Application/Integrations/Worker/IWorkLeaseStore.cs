namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>
/// The persistence boundary for worker leases over the inbox and outbox (requirement 3.15).
/// </summary>
/// <remarks>
/// <para>
/// Leasing records the attempt before any work starts, so a worker that crashes mid-item still
/// leaves attempt history. When a lease expires without being completed, the next
/// <see cref="LeaseNextAsync"/> reclaims the item, which makes processing crash-safe.
/// </para>
/// <para>
/// Every completion method names the lease owner and returns <see langword="false"/> when the
/// caller no longer holds the lease (it expired and was reclaimed), so a stale worker cannot
/// overwrite the newer holder's result. Items of a connection that is disabled, or paused for
/// outgoing work, are never leased.
/// </para>
/// </remarks>
public interface IWorkLeaseStore
{
    /// <summary>Leases up to <paramref name="batchSize"/> ready or expired-lease items.</summary>
    /// <param name="leaseOwner">The worker instance taking the lease.</param>
    /// <param name="nowUtc">The current instant.</param>
    /// <param name="leaseExpiresAtUtc">The instant the new leases expire.</param>
    /// <param name="batchSize">The largest number of items to lease.</param>
    /// <param name="maxTotalAttempts">Items that already received this many attempts are not leased.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The leased items, oldest first.</returns>
    Task<IReadOnlyList<LeasedWorkItem>> LeaseNextAsync(
        string leaseOwner,
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        int batchSize,
        int maxTotalAttempts,
        CancellationToken cancellationToken);

    /// <summary>Marks a leased item as successfully processed and clears its lease.</summary>
    /// <param name="item">The leased item.</param>
    /// <param name="leaseOwner">The worker instance that holds the lease.</param>
    /// <param name="nowUtc">The current instant.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when updated; <see langword="false"/> when the lease is no longer held.</returns>
    Task<bool> CompleteAsync(
        LeasedWorkItem item,
        string leaseOwner,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    /// <summary>Marks a leased item as failed and not eligible for another attempt before <paramref name="retryAtUtc"/>.</summary>
    /// <param name="item">The leased item.</param>
    /// <param name="leaseOwner">The worker instance that holds the lease.</param>
    /// <param name="retryAtUtc">The earliest instant of the next attempt.</param>
    /// <param name="nowUtc">The current instant.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when updated; <see langword="false"/> when the lease is no longer held.</returns>
    Task<bool> RescheduleAsync(
        LeasedWorkItem item,
        string leaseOwner,
        DateTimeOffset retryAtUtc,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns a leased item to its ready state without counting the attempt, because nothing was
    /// attempted.
    /// </summary>
    /// <param name="item">The leased item.</param>
    /// <param name="leaseOwner">The worker instance that holds the lease.</param>
    /// <param name="nowUtc">The current instant.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when updated; <see langword="false"/> when the lease is no longer held.</returns>
    Task<bool> ReleaseAsync(
        LeasedWorkItem item,
        string leaseOwner,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}
