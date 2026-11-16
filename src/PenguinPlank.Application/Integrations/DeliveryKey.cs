namespace PenguinPlank.Application.Integrations;

/// <summary>
/// The provider-supplied key that uniquely identifies one inbound notification delivery. Two
/// deliveries with the same key are the same notification and are deduplicated (requirement 3.8).
/// </summary>
/// <remarks>
/// An immutable value object that validates its invariants at construction: the key must be
/// non-empty and fit the persisted <c>IntegrationInbox.DeliveryKey</c> column. A malformed key is
/// a boundary mistake, so the constructor throws (coding-standards §5).
/// </remarks>
public readonly record struct DeliveryKey
{
    /// <summary>The maximum length, matching the persisted <c>IntegrationInbox.DeliveryKey</c> column.</summary>
    public const int MaxLength = 200;

    /// <summary>Creates a validated delivery key.</summary>
    /// <param name="value">The provider delivery key; non-empty and at most <see cref="MaxLength"/> characters.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="value"/> is null, whitespace, or longer than <see cref="MaxLength"/>.
    /// </exception>
    public DeliveryKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new System.ArgumentException(
                "A delivery key must be a non-empty value.",
                nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new System.ArgumentException(
                $"A delivery key must be at most {MaxLength} characters.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>The underlying key text.</summary>
    public string Value { get; }

    /// <summary>Returns the underlying key text.</summary>
    public override string ToString() => Value;
}
