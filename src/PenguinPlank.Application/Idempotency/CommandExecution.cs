namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// The outcome of running a mutating command body once inside the idempotency pipeline: the
/// business value to return to the caller together with the opaque <see cref="ResultRef"/> that is
/// persisted so an identical-payload retry can replay it (requirement A4 §6.4).
/// </summary>
/// <typeparam name="TValue">The business value the command produces (for example a created id).</typeparam>
/// <remarks>
/// This is an immutable value object (coding-standards §1). The command body returns both parts so
/// the pipeline can record the reference without having to understand the business value's shape.
/// Construct it only via <see cref="IdempotencyPipeline"/>-consumed command bodies.
/// </remarks>
public readonly record struct CommandExecution<TValue>
{
    /// <summary>Creates a command execution carrying the business value and its stored reference.</summary>
    /// <param name="value">The business value to return to the caller.</param>
    /// <param name="resultReference">The opaque reference persisted for replay.</param>
    public CommandExecution(TValue value, ResultRef resultReference)
    {
        Value = value;
        ResultReference = resultReference;
    }

    /// <summary>The business value produced by the command.</summary>
    public TValue Value { get; }

    /// <summary>The opaque reference persisted so an identical-payload retry replays this result.</summary>
    public ResultRef ResultReference { get; }
}
