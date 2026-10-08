namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// The logical operation a mutating command performs (for example <c>"CreateVariant"</c>),
/// persisted alongside the idempotency key so a stored record is attributable to the command
/// that produced it (requirement A4 §6.2).
/// </summary>
/// <remarks>
/// This is an immutable value object (coding-standards §1). It validates that the name is
/// non-empty and fits the persisted <c>IdempotencyRecord.Operation</c> column (128 characters);
/// a malformed name is a programming mistake, so the constructor throws.
/// </remarks>
public readonly record struct Operation
{
    /// <summary>
    /// The maximum length of an operation name, matching the persisted
    /// <c>IdempotencyRecord.Operation</c> column.
    /// </summary>
    public const int MaxLength = 128;

    /// <summary>Creates a validated operation name.</summary>
    /// <param name="name">The logical operation name; must be non-empty and at most <see cref="MaxLength"/> characters.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="name"/> is null, empty, whitespace, or longer than
    /// <see cref="MaxLength"/>.
    /// </exception>
    public Operation(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new System.ArgumentException(
                "An operation name must be a non-empty value.",
                nameof(name));
        }

        if (name.Length > MaxLength)
        {
            throw new System.ArgumentException(
                $"An operation name must be at most {MaxLength} characters.",
                nameof(name));
        }

        Name = name;
    }

    /// <summary>The logical operation name.</summary>
    public string Name { get; }

    /// <summary>Returns the operation name.</summary>
    public override string ToString() => Name;
}
