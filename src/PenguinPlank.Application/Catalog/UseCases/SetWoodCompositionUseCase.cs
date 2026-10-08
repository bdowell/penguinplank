using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Catalog.UseCases;

/// <summary>
/// Replaces the wood composition of a variant or piece, rejecting any supplied proportion outside
/// the inclusive 0–100 range and refusing a change against archived master data (requirements R01
/// §1.5, §1.8; R10 §2.2).
/// </summary>
/// <remarks>
/// <para>
/// The use case validates every supplied proportion with the pure
/// <see cref="WoodCompositionPolicy"/>, loads the target's active flag through the narrow
/// <see cref="ICatalogWoodStore"/>, applies the archived guard with
/// <see cref="ArchivedMasterDataPolicy"/>, and only on acceptance replaces the composition
/// atomically. An empty component set clears the composition. On rejection the use case returns the
/// policy's failure and leaves the composition unchanged (coding-standards §1).
/// </para>
/// </remarks>
public sealed class SetWoodCompositionUseCase
{
    private static readonly string[] s_woodCompositionChangeNames = ["WoodComposition"];

    private readonly ICatalogWoodStore _woodStore;
    private readonly IAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the use case with its injected boundaries.</summary>
    /// <param name="woodStore">The wood-composition persistence boundary used to confirm the target and replace the composition.</param>
    /// <param name="auditSink">The audit boundary a successful replacement records a summary through.</param>
    /// <param name="timeProvider">The injected clock supplying the mutation instant.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public SetWoodCompositionUseCase(
        ICatalogWoodStore woodStore,
        IAuditSink auditSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(woodStore);
        ArgumentNullException.ThrowIfNull(auditSink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _woodStore = woodStore;
        _auditSink = auditSink;
        _timeProvider = timeProvider;
    }

    /// <summary>Executes the set-wood-composition orchestration.</summary>
    /// <param name="request">The target record and the full desired set of components.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A success, or a typed failure carrying the rejecting policy's <see cref="ErrorCode"/> (an
    /// invalid proportion, an archived target, or an unknown target).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<Result> ExecuteAsync(
        SetWoodCompositionRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        // 1. Validate every supplied proportion with the pure policy (no I/O yet).
        Result proportionResult = WoodCompositionPolicy.Validate(request.Components.Select(component => component.Proportion));
        if (proportionResult.IsFailure)
        {
            return proportionResult;
        }

        // 2. Load the target's active flag and apply the archived guard.
        bool? targetActive = await _woodStore
            .GetTargetActiveAsync(request.Target, cancellationToken)
            .ConfigureAwait(false);

        if (targetActive is null)
        {
            return Result.Failure(ErrorCode.Validation, "The target variant or piece does not exist.");
        }

        Result archivedResult = ArchivedMasterDataPolicy.CanTransact(targetActive.Value);
        if (archivedResult.IsFailure)
        {
            return archivedResult;
        }

        // 3. Accepted: replace the composition atomically and audit the change.
        DateTimeOffset updatedAtUtc = _timeProvider.GetUtcNow();
        await _woodStore
            .ReplaceCompositionAsync(request.Target, request.Components, updatedAtUtc, cancellationToken)
            .ConfigureAwait(false);

        await _auditSink
            .RecordAsync(
                AuditEntryFactory.CreateEntry(
                    new AuditChange(
                        EntityTypeName(request.Target.Kind),
                        request.Target.Id,
                        AuditOperation.Updated,
                        s_woodCompositionChangeNames),
                    actor,
                    updatedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    private static string EntityTypeName(CatalogEntityKind kind) =>
        kind switch
        {
            CatalogEntityKind.Variant => nameof(ProductVariant),
            CatalogEntityKind.Piece => nameof(ProductPiece),
            _ => nameof(Product),
        };
}
