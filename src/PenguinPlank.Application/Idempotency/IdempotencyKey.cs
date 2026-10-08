namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// A caller-supplied idempotency key for a mutating command. It is a non-empty, bounded
/// string that uniquely identifies a single logical command attempt so that a retry carrying
/// the same key is recognized as a replay rather than a new effect (requirement A4 §6.2).
/// </summary>
/// <remarks>
/// <para>
/// This is an immutable value object with no I/O (coding-standards §1). It validates its own
/// invariants at construction: a key must be non-empty and must fit the persisted column
/// (<c>IdempotencyRecord.Key</c>, 200 characters), so a malformed key is rejected at the
/// boundary rather than failing later at the database. A null, empty, whitespace, or
/// over-length key is a caller/programming mistake — not an expected business failure — so the
/// constructor throws.
/// </para>
/// </remarks>
public readonly record struct IdempotencyKey
{
    /// <summary>
    /// The maximum length of a key, matching the persisted <c>IdempotencyRecord.Key</c> column
    /// so a valid key always fits storage.
    /// </summary>
    public const int MaxLength = 200;

    /// <summary>Creates a validated idempotency key.</summary>
    /// <param name="value">The caller-supplied key; must be non-empty and at most <see cref="MaxLength"/> characters.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="value"/> is null, empty, whitespace, or longer than
    /// <see cref="MaxLength"/>.
    /// </exception>
    public IdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new System.ArgumentException(
                "An idempotency key must be a non-empty value.",
                nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new System.ArgumentException(
                $"An idempotency key must be at most {MaxLength} characters.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>The underlying key text.</summary>
    public string Value { get; }

    /// <summary>Returns the underlying key text.</summary>
    public override string ToString() => Value;
}
