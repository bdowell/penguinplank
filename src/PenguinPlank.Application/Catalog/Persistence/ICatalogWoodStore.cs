namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The narrow persistence boundary a set-wood-composition use case depends on to <b>confirm the
/// target variant or piece is active</b> and to <b>replace its wood composition</b> once every
/// proportion has been accepted.
/// </summary>
/// <remarks>
/// <para>
/// This interface exists so the set-wood-composition use case (task 8.2) can be orchestrated and
/// unit-tested against a controllable substitute (task 8.4) while Infrastructure provides the EF
/// Core implementation (task 8.3). It is a focused, responsibility-named boundary (coding-standards
/// §2, §6). The use case first validates every supplied proportion with the pure
/// <c>WoodCompositionPolicy</c> and the archived guard with <c>ArchivedMasterDataPolicy</c> before
/// calling <see cref="ReplaceCompositionAsync"/>; the business rules never live in the adapter
/// (coding-standards §1, §3).
/// </para>
/// <para>
/// Every method takes and propagates a <see cref="CancellationToken"/>. The active-state read
/// returns <see langword="null"/> for an absent target and does not throw for absence. No EF entity
/// crosses the boundary — only the Application component and reference types (coding-standards §2,
/// §3).
/// </para>
/// </remarks>
public interface ICatalogWoodStore
{
    /// <summary>
    /// Determines whether the target variant or piece exists and is active, or
    /// <see langword="null"/> when no record matches.
    /// </summary>
    /// <param name="target">The variant or piece whose composition is being set.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> when the target is active, <see langword="false"/> when archived, or
    /// <see langword="null"/> when no record matches.
    /// </returns>
    Task<bool?> GetTargetActiveAsync(CatalogRef target, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the target's wood composition with exactly the accepted component set and records
    /// the mutation instant. An empty set clears the composition.
    /// </summary>
    /// <param name="target">The variant or piece whose composition is being replaced.</param>
    /// <param name="components">The full accepted set of components; empty clears the composition.</param>
    /// <param name="updatedAtUtc">The mutation instant, resolved from the injected time provider.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the composition has been replaced.</returns>
    Task ReplaceCompositionAsync(
        CatalogRef target,
        IReadOnlyList<WoodComponent> components,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
}
