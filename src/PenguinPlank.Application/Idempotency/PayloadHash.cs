namespace PenguinPlank.Application.Idempotency;

/// <summary>
/// A stable hash of a mutating command's request payload. Reusing an idempotency key with an
/// <em>identical</em> payload hash replays the stored result; reusing it with a <em>different</em>
/// hash is a conflict (requirements A4 §6.3, §6.4).
/// </summary>
/// <remarks>
/// <para>
/// This is an immutable value object (coding-standards §1). The hash is produced by the pure
/// <see cref="PayloadHasher"/> as a lowercase SHA-256 hex string, so it is deterministic for a
/// given payload and fits the persisted <c>IdempotencyRecord.PayloadHash</c> column
/// (128 characters). Equality is case-insensitive on the hex text so a value compares equal
/// regardless of how the hex was cased, which is what the replay-versus-conflict decision relies
/// on (<see cref="IdempotencyDecision"/>).
/// </para>
/// </remarks>
public readonly record struct PayloadHash
{
    /// <summary>
    /// The maximum length of a stored hash, matching the persisted
    /// <c>IdempotencyRecord.PayloadHash</c> column.
    /// </summary>
    public const int MaxLength = 128;

    /// <summary>Creates a payload hash from an already-computed hex value.</summary>
    /// <param name="value">The hash hex text; must be non-empty and at most <see cref="MaxLength"/> characters.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="value"/> is null, empty, whitespace, or longer than
    /// <see cref="MaxLength"/>.
    /// </exception>
    public PayloadHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new System.ArgumentException(
                "A payload hash must be a non-empty value.",
                nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new System.ArgumentException(
                $"A payload hash must be at most {MaxLength} characters.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>The hash hex text.</summary>
    public string Value { get; }

    /// <summary>Compares the hash hex text case-insensitively.</summary>
    /// <param name="other">The other hash to compare with.</param>
    /// <returns><see langword="true"/> when the two hex strings are equal ignoring case.</returns>
    public bool Equals(PayloadHash other) =>
        string.Equals(Value, other.Value, System.StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns a case-insensitive hash code consistent with <see cref="Equals(PayloadHash)"/>.</summary>
    public override int GetHashCode() =>
        Value is null ? 0 : System.StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <summary>Returns the hash hex text.</summary>
    public override string ToString() => Value;
}
