namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// The result of beginning an idempotent command: whether the key is new, a replay of a stored
/// result, or a conflict with a differing payload (requirements A4 §6.2–§6.4).
/// </summary>
/// <remarks>
/// <para>
/// This is an immutable value carrying a discriminated outcome (coding-standards §1). Construct
/// it only through the <see cref="New"/>, <see cref="Replay(ResultRef?)"/>, and
/// <see cref="Conflict"/> factories so an outcome is always internally consistent — a
/// <see cref="IdempotencyOutcomeKind.Replay"/> always carries a stored
/// <see cref="StoredResult"/>, and the other kinds never do.
/// </para>
/// <para>
/// A stored result may legitimately be absent on a replay: the original command may have reserved
/// the key but not yet completed. <see cref="StoredResult"/> is therefore nullable and the caller
/// handles the "recorded but not yet completed" case explicitly rather than assuming a value.
/// </para>
/// </remarks>
public readonly record struct IdempotencyOutcome
{
    private IdempotencyOutcome(IdempotencyOutcomeKind kind, ResultRef? storedResult)
    {
        Kind = kind;
        StoredResult = storedResult;
    }

    /// <summary>How the begin resolved: new, replay, or conflict.</summary>
    public IdempotencyOutcomeKind Kind { get; }

    /// <summary>
    /// The stored result reference to replay, present only when <see cref="Kind"/> is
    /// <see cref="IdempotencyOutcomeKind.Replay"/> and the original command had completed; otherwise
    /// <see langword="null"/>.
    /// </summary>
    public ResultRef? StoredResult { get; }

    /// <summary><see langword="true"/> when the key is new and the caller should execute the command once.</summary>
    public bool IsNew => Kind == IdempotencyOutcomeKind.New;

    /// <summary><see langword="true"/> when the caller should replay the stored result without repeating the effect.</summary>
    public bool IsReplay => Kind == IdempotencyOutcomeKind.Replay;

    /// <summary><see langword="true"/> when the key was reused with a differing payload and the request is a conflict.</summary>
    public bool IsConflict => Kind == IdempotencyOutcomeKind.Conflict;

    /// <summary>A fresh key was reserved; the caller executes the command once.</summary>
    public static IdempotencyOutcome New { get; } =
        new(IdempotencyOutcomeKind.New, storedResult: null);

    /// <summary>The key was reused with a differing payload; the request is a conflict.</summary>
    public static IdempotencyOutcome Conflict { get; } =
        new(IdempotencyOutcomeKind.Conflict, storedResult: null);

    /// <summary>
    /// The key was reused with an identical payload; replay the stored result (which may be
    /// <see langword="null"/> if the original command had not yet completed).
    /// </summary>
    /// <param name="storedResult">The stored result reference to replay, or <see langword="null"/>.</param>
    /// <returns>A replay outcome carrying <paramref name="storedResult"/>.</returns>
    public static IdempotencyOutcome Replay(ResultRef? storedResult) =>
        new(IdempotencyOutcomeKind.Replay, storedResult);
}
