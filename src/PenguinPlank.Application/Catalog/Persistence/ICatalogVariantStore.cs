namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The narrow persistence boundary a variant use case depends on to <b>load the existing-state
/// facts</b> its pure policies decide against and to <b>persist the decided outcome</b>: the
/// existing SKUs/barcodes for a uniqueness check, a variant's active/tracking/stock-history facts,
/// the insert of an accepted new variant, and the active-flag flip that archives or reactivates a
/// variant.
/// </summary>
/// <remarks>
/// <para>
/// This interface exists so the catalog use cases (task 8.2) can be orchestrated and unit-tested
/// against a controllable substitute (task 8.4) while Infrastructure provides the EF Core
/// implementation (task 8.3). It is a focused, responsibility-named boundary — deliberately not a
/// generic repository wrapping every <c>DbSet</c> (coding-standards §2, §6). Its fact-loading
/// reads feed the <em>pure</em> Domain decisions (<c>SkuPolicy</c>, <c>ArchivedMasterDataPolicy</c>,
/// <c>TrackingModeChangePolicy</c>); the business rules stay in the use case and the policies, never
/// in the adapter (coding-standards §1, §3).
/// </para>
/// <para>
/// Every method takes and propagates a <see cref="CancellationToken"/> and returns a
/// <see cref="System.Threading.Tasks.Task"/>; the fact-loading reads return a snapshot or
/// <see langword="null"/> for an absent record and do not throw for absence. No EF entity,
/// <c>IQueryable</c>, or <c>DbContext</c> crosses the boundary — only the Application snapshot and
/// record types (coding-standards §2, §3). Time is supplied by the use case from an injected
/// <see cref="System.TimeProvider"/> and carried on the record to insert; the adapter reads no
/// clock of its own.
/// </para>
/// </remarks>
public interface ICatalogVariantStore
{
    /// <summary>
    /// Loads the existing SKUs and barcodes a candidate variant's uniqueness is checked against,
    /// excluding the edited variant's own current codes when
    /// <see cref="VariantUniquenessQuery.ExcludeVariantId"/> is set.
    /// </summary>
    /// <param name="query">The candidate codes and the optional self-exclusion.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The existing SKUs and barcodes in scope.</returns>
    Task<CatalogUniquenessSnapshot> GetUniquenessSnapshotAsync(VariantUniquenessQuery query, CancellationToken cancellationToken);

    /// <summary>Loads the existing-state facts for a variant, or <see langword="null"/> when none matches.</summary>
    /// <param name="variantId">The variant whose facts are requested.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The variant's facts, or <see langword="null"/> when no variant matches.</returns>
    Task<VariantFacts?> GetVariantFactsAsync(Guid variantId, CancellationToken cancellationToken);

    /// <summary>Determines whether a product exists and is active, or <see langword="null"/> when no product matches.</summary>
    /// <param name="productId">The owning product the new variant references.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> when the product is active, <see langword="false"/> when archived, or
    /// <see langword="null"/> when no product matches.
    /// </returns>
    Task<bool?> GetProductActiveAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Inserts an accepted new variant exactly as the use case decided it.</summary>
    /// <param name="record">The fully decided variant values to persist.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the variant has been persisted.</returns>
    Task InsertVariantAsync(NewVariantRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a variant's active flag, archiving (<paramref name="isActive"/> <see langword="false"/>)
    /// or reactivating (<see langword="true"/>) it, and records the mutation instant.
    /// </summary>
    /// <param name="variantId">The variant whose active flag is changing.</param>
    /// <param name="isActive">The desired active state.</param>
    /// <param name="updatedAtUtc">The mutation instant, resolved from the injected time provider.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the active flag has been persisted.</returns>
    Task SetVariantActiveAsync(Guid variantId, bool isActive, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken);
}
