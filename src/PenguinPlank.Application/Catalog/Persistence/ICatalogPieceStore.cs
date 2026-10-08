namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The narrow persistence boundary a create-piece use case depends on to <b>load the owning
/// variant's facts and the existing piece codes</b> its pure policies decide against and to
/// <b>persist the accepted piece</b>.
/// </summary>
/// <remarks>
/// <para>
/// This interface exists so the create-piece use case (task 8.2) can be orchestrated and
/// unit-tested against a controllable substitute (task 8.4) while Infrastructure provides the EF
/// Core implementation (task 8.3). It is a focused, responsibility-named boundary — not a generic
/// repository (coding-standards §2, §6). Its fact-loading read feeds the pure Domain decisions
/// (<c>ArchivedMasterDataPolicy</c>, <c>PieceCodePolicy</c>, <c>CareVersionResolver</c>); the
/// business rules stay in the use case and the policies (coding-standards §1, §3).
/// </para>
/// <para>
/// Every method takes and propagates a <see cref="CancellationToken"/>; the fact-loading read
/// returns a snapshot or <see langword="null"/> for an absent variant and does not throw for
/// absence. No EF entity, <c>IQueryable</c>, or <c>DbContext</c> crosses the boundary — only the
/// Application snapshot/record types and pure Domain values (coding-standards §2, §3).
/// </para>
/// </remarks>
public interface ICatalogPieceStore
{
    /// <summary>
    /// Loads the owning variant's facts, its care-profile versions, and the catalog's existing
    /// piece codes, or <see langword="null"/> when no variant matches.
    /// </summary>
    /// <param name="variantId">The owning variant the new piece references.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The creation facts, or <see langword="null"/> when no variant matches.</returns>
    Task<PieceCreationFacts?> GetPieceCreationFactsAsync(Guid variantId, CancellationToken cancellationToken);

    /// <summary>Inserts an accepted new piece exactly as the use case decided it.</summary>
    /// <param name="record">The fully decided piece values to persist.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the piece has been persisted.</returns>
    Task InsertPieceAsync(NewPieceRecord record, CancellationToken cancellationToken);
}
