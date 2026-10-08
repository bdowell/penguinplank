namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// The persistence boundary for idempotency records: reserve a key for a new command, or report
/// a replay or conflict for a reused one, and record the result of a completed command
/// (requirements A4 §6.2–§6.4).
/// </summary>
/// <remarks>
/// <para>
/// This is a narrow, responsibility-named boundary interface defined in Application and
/// implemented in Infrastructure over EF Core (coding-standards §2, §3). The implementation
/// relies on the <b>unique database index on the idempotency key</b> to serialize concurrent
/// same-key requests: exactly one request inserts the reserving record (observing
/// <see cref="IdempotencyOutcomeKind.New"/>); any racing insert fails the uniqueness constraint
/// and is resolved by re-reading the now-committed record and applying
/// <see cref="IdempotencyDecision"/> — a replay for an identical payload or a conflict for a
/// differing one.
/// </para>
/// <para>
/// Both methods propagate the <see cref="CancellationToken"/>. <see cref="BeginAsync"/> returns a
/// typed <see cref="IdempotencyOutcome"/> for the expected new/replay/conflict cases rather than
/// throwing; unexpected failures surface as exceptions (coding-standards §6).
/// </para>
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Reserves <paramref name="key"/> for a new command, or reports that it is a replay or a
    /// conflict with a prior use of the same key.
    /// </summary>
    /// <param name="key">The caller-supplied idempotency key.</param>
    /// <param name="caller">The authenticated caller issuing the command.</param>
    /// <param name="operation">The logical operation the command performs.</param>
    /// <param name="payloadHash">The stable hash of the request payload.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task producing the <see cref="IdempotencyOutcome"/>: <see cref="IdempotencyOutcomeKind.New"/>
    /// when the key was reserved, <see cref="IdempotencyOutcomeKind.Replay"/> (with the stored
    /// result, if any) when it was reused with an identical payload, or
    /// <see cref="IdempotencyOutcomeKind.Conflict"/> when it was reused with a differing payload.
    /// </returns>
    Task<IdempotencyOutcome> BeginAsync(
        IdempotencyKey key,
        Caller caller,
        Operation operation,
        PayloadHash payloadHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the result of a command that was begun with <see cref="IdempotencyOutcomeKind.New"/>,
    /// so a later identical-payload retry replays <paramref name="result"/> without repeating the
    /// effect (requirement A4 §6.4).
    /// </summary>
    /// <param name="key">The idempotency key of the completed command.</param>
    /// <param name="result">A reference to the command's stored result.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the result reference is persisted.</returns>
    Task CompleteAsync(
        IdempotencyKey key,
        ResultRef result,
        CancellationToken cancellationToken);
}
