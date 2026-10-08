using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Abstractions.Concurrency;

/// <summary>
/// Applies a pending edit to a mutable aggregate under optimistic concurrency, honoring the
/// client's <c>If-Match</c> ETag so a stale edit never silently overwrites newer state.
/// </summary>
/// <remarks>
/// <para>
/// This is the reusable mechanism behind requirement A4 §6.5/§6.6 and design invariant 14
/// ("stale edit never overwrites"). A use case loads the aggregate, mutates it, and then calls
/// <see cref="ExecuteAsync"/> with the <c>If-Match</c> token the client presented. The
/// implementation forces the persistence layer to issue a conditional
/// <c>UPDATE ... WHERE rowversion = @ifMatch</c>: if the row has moved on since the client read
/// it, zero rows match and the operation fails with <see cref="ErrorCode.StaleVersion"/> (mapped
/// to HTTP 412 with a refresh prompt) instead of overwriting the newer value.
/// </para>
/// <para>
/// The interface is deliberately narrow and named for its single responsibility (coding-standards
/// §2). It is an external-boundary seam: the concrete implementation lives in Infrastructure
/// because the conditional update is an EF Core concern, and it is registered <b>scoped</b> in the
/// composition root because it captures the scoped <c>DbContext</c>. Keeping the contract in the
/// Application layer lets use cases be tested with a controllable substitute without starting the
/// application or touching SQL (coding-standards §1, §3).
/// </para>
/// </remarks>
public interface IConcurrentUpdateExecutor
{
    /// <summary>
    /// Commits the already-applied changes to <paramref name="entity"/>, requiring the current
    /// stored row version to equal <paramref name="expectedRowVersion"/> (the client's
    /// <c>If-Match</c> token).
    /// </summary>
    /// <param name="entity">
    /// The tracked, already-mutated mutable aggregate to persist. Its in-memory
    /// <see cref="VersionedEntity.RowVersion"/> is ignored for the concurrency check in favor of
    /// <paramref name="expectedRowVersion"/>, so a caller cannot bypass the check by having
    /// reloaded a fresh version.
    /// </param>
    /// <param name="expectedRowVersion">
    /// The row version the client expects to overwrite — the decoded <c>If-Match</c> ETag. The
    /// conditional update targets exactly this value.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A successful <see cref="Result"/> when exactly the expected version was updated; a
    /// failure carrying <see cref="ErrorCode.StaleVersion"/> when the stored version had already
    /// advanced (the edit is not applied).
    /// </returns>
    Task<Result> ExecuteAsync(
        VersionedEntity entity,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken);
}
