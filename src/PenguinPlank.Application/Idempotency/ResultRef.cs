namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// A reference to the stored result of the original command, recorded by
/// <see cref="IIdempotencyStore.CompleteAsync"/> and replayed on an identical-payload retry so
/// the effect is not repeated (requirement A4 §6.4).
/// </summary>
/// <remarks>
/// <para>
/// This is an immutable value object (coding-standards §1). The reference is an opaque,
/// caller-defined token (for example the created resource's identifier or a stored
/// representation key) — not the business result itself. It must fit the persisted
/// <c>IdempotencyRecord.ResultReference</c> column (400 characters). A null, empty, or
/// over-length reference is a programming mistake, so the constructor throws.
/// </para>
/// </remarks>
public readonly record struct ResultRef
{
    /// <summary>
    /// The maximum length of a stored result reference, matching the persisted
    /// <c>IdempotencyRecord.ResultReference</c> column.
    /// </summary>
    public const int MaxLength = 400;

    /// <summary>Creates a validated result reference.</summary>
    /// <param name="value">The opaque reference token; must be non-empty and at most <see cref="MaxLength"/> characters.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="value"/> is null, empty, whitespace, or longer than
    /// <see cref="MaxLength"/>.
    /// </exception>
    public ResultRef(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new System.ArgumentException(
                "A result reference must be a non-empty value.",
                nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new System.ArgumentException(
                $"A result reference must be at most {MaxLength} characters.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>The opaque reference token.</summary>
    public string Value { get; }

    /// <summary>Returns the reference token.</summary>
    public override string ToString() => Value;
}
