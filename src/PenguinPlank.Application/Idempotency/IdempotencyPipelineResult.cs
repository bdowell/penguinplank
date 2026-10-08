using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// The outcome of running a mutating command through <see cref="IdempotencyPipeline"/>: the
/// command executed once, the request replayed a prior result without repeating the effect, or
/// the key was reused with a differing payload and the request is a conflict
/// (requirements A4 §6.2–§6.4).
/// </summary>
/// <typeparam name="TValue">The business value the command produces on first execution.</typeparam>
/// <remarks>
/// <para>
/// This is an immutable, discriminated value (coding-standards §1). It distinguishes the three
/// cases so the API boundary can map each to the correct response: a first execution returns the
/// business <see cref="Value"/>; a replay returns the stored <see cref="StoredResult"/> (the
/// original effect is not repeated); a conflict is surfaced as the stable
/// <see cref="ErrorCode.IdempotencyConflict"/> failure, which maps to HTTP 409.
/// </para>
/// <para>
/// On a first execution the pipeline holds the real <typeparamref name="TValue"/>. On a replay it
/// holds only the persisted <see cref="ResultRef"/> (the business value was produced by an earlier
/// request and is not reconstructed here), so <see cref="Value"/> is unavailable and
/// <see cref="StoredResult"/> is the authoritative handle to the original result.
/// </para>
/// </remarks>
public readonly record struct IdempotencyPipelineResult<TValue>
{
    private readonly TValue _value;

    internal IdempotencyPipelineResult(
        IdempotencyOutcomeKind kind,
        TValue value,
        ResultRef? storedResult,
        BusinessError? conflict)
    {
        Kind = kind;
        _value = value;
        StoredResult = storedResult;
        Conflict = conflict;
    }

    /// <summary>How the request resolved: executed, replayed, or conflicted.</summary>
    public IdempotencyOutcomeKind Kind { get; }

    /// <summary><see langword="true"/> when the command executed once on this request.</summary>
    public bool WasExecuted => Kind == IdempotencyOutcomeKind.New;

    /// <summary><see langword="true"/> when a prior result was replayed without repeating the effect.</summary>
    public bool WasReplayed => Kind == IdempotencyOutcomeKind.Replay;

    /// <summary><see langword="true"/> when the key was reused with a differing payload (a conflict).</summary>
    public bool WasConflict => Kind == IdempotencyOutcomeKind.Conflict;

    /// <summary>
    /// The business value, available only when <see cref="WasExecuted"/> is <see langword="true"/>.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when accessed on a replay or conflict result, which carry no freshly produced value.
    /// </exception>
    public TValue Value =>
        WasExecuted
            ? _value
            : throw new System.InvalidOperationException(
                "A replayed or conflicting idempotency result has no freshly produced value; use StoredResult or Conflict.");

    /// <summary>
    /// The persisted result reference, present on an executed result and on a replay (when the
    /// original command had completed); <see langword="null"/> otherwise.
    /// </summary>
    public ResultRef? StoredResult { get; }

    /// <summary>
    /// The conflict error, present only when <see cref="WasConflict"/> is <see langword="true"/>.
    /// </summary>
    public BusinessError? Conflict { get; }
}

/// <summary>
/// Factory methods that produce an <see cref="IdempotencyPipelineResult{TValue}"/> for each of the
/// three outcomes.
/// </summary>
/// <remarks>
/// The factories live on this non-generic companion type rather than on the generic result struct
/// itself, so a caller writes <c>IdempotencyPipelineResult.Executed(value, reference)</c> and the
/// generic value type is inferred. This mirrors the <see cref="Result"/> / <see cref="Result{T}"/>
/// pairing already used in the Domain and keeps static factories off the generic type
/// (coding-standards §5).
/// </remarks>
public static class IdempotencyPipelineResult
{
    /// <summary>Creates an executed result carrying the business value and its stored reference.</summary>
    /// <typeparam name="TValue">The business value the command produced.</typeparam>
    /// <param name="value">The business value produced by the command.</param>
    /// <param name="storedResult">The reference persisted for later replay.</param>
    /// <returns>An executed pipeline result.</returns>
    public static IdempotencyPipelineResult<TValue> Executed<TValue>(TValue value, ResultRef storedResult) =>
        new(IdempotencyOutcomeKind.New, value, storedResult, conflict: null);

    /// <summary>Creates a replayed result carrying the stored reference (which may be absent).</summary>
    /// <typeparam name="TValue">The business value the original command produced.</typeparam>
    /// <param name="storedResult">The prior result reference to replay, or <see langword="null"/>.</param>
    /// <returns>A replayed pipeline result.</returns>
    public static IdempotencyPipelineResult<TValue> Replayed<TValue>(ResultRef? storedResult) =>
        new(IdempotencyOutcomeKind.Replay, value: default!, storedResult, conflict: null);

    /// <summary>Creates a conflict result carrying the stable idempotency-conflict error.</summary>
    /// <typeparam name="TValue">The business value the command would have produced.</typeparam>
    /// <param name="error">The conflict business error (code <see cref="ErrorCode.IdempotencyConflict"/>).</param>
    /// <returns>A conflict pipeline result.</returns>
    public static IdempotencyPipelineResult<TValue> ConflictWith<TValue>(BusinessError error) =>
        new(IdempotencyOutcomeKind.Conflict, value: default!, storedResult: null, conflict: error);
}
