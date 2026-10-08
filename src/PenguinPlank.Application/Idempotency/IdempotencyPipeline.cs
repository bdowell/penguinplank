using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// A reusable component that wraps a mutating command with idempotency semantics: it runs the
/// command body <b>once</b> for a new key and replays the stored result for a repeated key with an
/// identical payload, or rejects a repeated key with a differing payload as a conflict
/// (requirements A4 §6.2–§6.4).
/// </summary>
/// <remarks>
/// <para>
/// Use cases and endpoints call <see cref="ExecuteAsync{TValue}"/> to make a command idempotent
/// without re-implementing the begin/complete protocol. The pipeline depends only on the
/// <see cref="IIdempotencyStore"/> boundary (injected via the constructor — coding-standards §2),
/// so it is testable with a controllable fake store and performs no I/O of its own beyond the
/// store calls. The replay-versus-conflict business rule itself lives in the pure
/// <see cref="IdempotencyDecision"/>, which the store applies.
/// </para>
/// <para>
/// <b>Protocol.</b> <see cref="ExecuteAsync{TValue}"/> calls
/// <see cref="IIdempotencyStore.BeginAsync"/>. On <see cref="IdempotencyOutcomeKind.New"/> it
/// invokes the command body exactly once, then records the produced
/// <see cref="CommandExecution{TValue}.ResultReference"/> with
/// <see cref="IIdempotencyStore.CompleteAsync"/> so a later identical retry replays it. On
/// <see cref="IdempotencyOutcomeKind.Replay"/> it returns the stored result and never invokes the
/// body (no repeated effect). On <see cref="IdempotencyOutcomeKind.Conflict"/> it returns a
/// conflict carrying <see cref="ErrorCode.IdempotencyConflict"/> and never invokes the body.
/// </para>
/// </remarks>
public sealed class IdempotencyPipeline
{
    private readonly IIdempotencyStore _store;

    /// <summary>Creates the pipeline over the idempotency persistence boundary.</summary>
    /// <param name="store">The idempotency store that reserves keys and records results.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
    public IdempotencyPipeline(IIdempotencyStore store)
    {
        System.ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>
    /// Runs <paramref name="command"/> under the idempotency key, executing it once for a new key
    /// and replaying the stored result otherwise.
    /// </summary>
    /// <typeparam name="TValue">The business value the command produces on first execution.</typeparam>
    /// <param name="key">The caller-supplied idempotency key.</param>
    /// <param name="caller">The authenticated caller issuing the command.</param>
    /// <param name="operation">The logical operation the command performs.</param>
    /// <param name="payloadHash">The stable hash of the request payload.</param>
    /// <param name="command">
    /// The command body, invoked at most once (only when the key is new). It performs the business
    /// mutation and returns the value plus the <see cref="ResultRef"/> to persist for replay.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task producing an <see cref="IdempotencyPipelineResult{TValue}"/>: executed (with the
    /// value), replayed (with the stored reference), or a conflict.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
    public async Task<IdempotencyPipelineResult<TValue>> ExecuteAsync<TValue>(
        IdempotencyKey key,
        Caller caller,
        Operation operation,
        PayloadHash payloadHash,
        System.Func<CancellationToken, Task<CommandExecution<TValue>>> command,
        CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(command);

        IdempotencyOutcome outcome = await _store
            .BeginAsync(key, caller, operation, payloadHash, cancellationToken)
            .ConfigureAwait(false);

        switch (outcome.Kind)
        {
            case IdempotencyOutcomeKind.Replay:
                return IdempotencyPipelineResult.Replayed<TValue>(outcome.StoredResult);

            case IdempotencyOutcomeKind.Conflict:
                return IdempotencyPipelineResult.ConflictWith<TValue>(
                    new BusinessError(
                        ErrorCode.IdempotencyConflict,
                        "The idempotency key was already used with a different request payload."));

            case IdempotencyOutcomeKind.New:
                CommandExecution<TValue> execution =
                    await command(cancellationToken).ConfigureAwait(false);

                await _store
                    .CompleteAsync(key, execution.ResultReference, cancellationToken)
                    .ConfigureAwait(false);

                return IdempotencyPipelineResult.Executed(
                    execution.Value,
                    execution.ResultReference);

            default:
                // The outcome kind is produced only by the factories above; an unmapped value is a
                // programming mistake, not an expected business failure (coding-standards §6).
                throw new System.InvalidOperationException(
                    $"Unhandled idempotency outcome kind '{outcome.Kind}'.");
        }
    }
}
