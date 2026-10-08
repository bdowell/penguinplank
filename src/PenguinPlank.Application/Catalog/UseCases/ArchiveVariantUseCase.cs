using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Catalog.UseCases;

/// <summary>
/// Archives a <c>ProductVariant</c> (a soft retirement) and reactivates a previously archived one.
/// An archived variant stays readable but rejects new transactions (requirement R01 §1.8).
/// </summary>
/// <remarks>
/// <para>
/// Archiving is deliberately idempotent against the current state: archiving an already-archived
/// variant, or reactivating an already-active one, is a no-op that still succeeds, so a retried
/// request never fails spuriously. The use case loads the variant's facts through the narrow
/// <see cref="ICatalogVariantStore"/>, flips the active flag only when it differs, and records an
/// audit summary for an actual change. It makes the decision from ordinary loaded facts, then
/// orchestrates the side effect (coding-standards §1); it holds no EF type and reads no
/// <c>HttpContext</c> (coding-standards §2, §3).
/// </para>
/// </remarks>
public sealed class ArchiveVariantUseCase
{
    private static readonly string[] s_activeFlagChangeNames = [nameof(VariantFacts.IsActive)];

    private readonly ICatalogVariantStore _variantStore;
    private readonly IAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the use case with its injected boundaries.</summary>
    /// <param name="variantStore">The variant persistence boundary used to load facts and flip the active flag.</param>
    /// <param name="auditSink">The audit boundary a state change records a summary through.</param>
    /// <param name="timeProvider">The injected clock supplying the mutation instant.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public ArchiveVariantUseCase(
        ICatalogVariantStore variantStore,
        IAuditSink auditSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(variantStore);
        ArgumentNullException.ThrowIfNull(auditSink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _variantStore = variantStore;
        _auditSink = auditSink;
        _timeProvider = timeProvider;
    }

    /// <summary>Archives the referenced variant.</summary>
    /// <param name="variantId">The variant to archive.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success, or a failure when no variant matches.</returns>
    public Task<Result> ArchiveAsync(Guid variantId, ActorContext actor, CancellationToken cancellationToken) =>
        SetActiveAsync(variantId, isActive: false, actor, cancellationToken);

    /// <summary>Reactivates the referenced archived variant.</summary>
    /// <param name="variantId">The variant to reactivate.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A success, or a failure when no variant matches.</returns>
    public Task<Result> UnarchiveAsync(Guid variantId, ActorContext actor, CancellationToken cancellationToken) =>
        SetActiveAsync(variantId, isActive: true, actor, cancellationToken);

    private async Task<Result> SetActiveAsync(
        Guid variantId,
        bool isActive,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        VariantFacts? facts = await _variantStore
            .GetVariantFactsAsync(variantId, cancellationToken)
            .ConfigureAwait(false);

        if (facts is null)
        {
            return Result.Failure(ErrorCode.Validation, "The variant does not exist.");
        }

        if (facts.IsActive == isActive)
        {
            // Already in the requested state: a safe, idempotent no-op.
            return Result.Success();
        }

        DateTimeOffset updatedAtUtc = _timeProvider.GetUtcNow();
        await _variantStore
            .SetVariantActiveAsync(variantId, isActive, updatedAtUtc, cancellationToken)
            .ConfigureAwait(false);

        await _auditSink
            .RecordAsync(
                AuditEntryFactory.CreateEntry(
                    new AuditChange(
                        nameof(ProductVariant),
                        variantId,
                        AuditOperation.Updated,
                        s_activeFlagChangeNames),
                    actor,
                    updatedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
