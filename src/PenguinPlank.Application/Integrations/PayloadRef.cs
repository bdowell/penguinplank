namespace PenguinPlank.Application.Integrations;

/// <summary>
/// A reference to a stored notification payload. The payload itself is held outside the inbox
/// row; only this reference is persisted.
/// </summary>
/// <remarks>
/// An immutable value object. The reference must be non-empty and fit the persisted
/// <c>IntegrationInbox.PayloadReference</c> column.
/// </remarks>
public readonly record struct PayloadRef
{
    /// <summary>The maximum length, matching the persisted <c>IntegrationInbox.PayloadReference</c> column.</summary>
    public const int MaxLength = 400;

    /// <summary>Creates a validated payload reference.</summary>
    /// <param name="value">The payload reference; non-empty and at most <see cref="MaxLength"/> characters.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="value"/> is null, whitespace, or longer than <see cref="MaxLength"/>.
    /// </exception>
    public PayloadRef(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new System.ArgumentException(
                "A payload reference must be a non-empty value.",
                nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new System.ArgumentException(
                $"A payload reference must be at most {MaxLength} characters.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>The underlying reference text.</summary>
    public string Value { get; }

    /// <summary>Returns the underlying reference text.</summary>
    public override string ToString() => Value;
}
