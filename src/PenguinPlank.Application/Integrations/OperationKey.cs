namespace PenguinPlank.Application.Integrations;

/// <summary>
/// The caller-defined key that uniquely identifies one outgoing integration operation. An
/// operation is enqueued at most once per key (requirement 3.14).
/// </summary>
/// <remarks>
/// An immutable value object that validates its invariants at construction: the key must be
/// non-empty and fit the persisted <c>IntegrationOutbox.OperationKey</c> column.
/// </remarks>
public readonly record struct OperationKey
{
    /// <summary>The maximum length, matching the persisted <c>IntegrationOutbox.OperationKey</c> column.</summary>
    public const int MaxLength = 200;

    /// <summary>Creates a validated operation key.</summary>
    /// <param name="value">The operation key; non-empty and at most <see cref="MaxLength"/> characters.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="value"/> is null, whitespace, or longer than <see cref="MaxLength"/>.
    /// </exception>
    public OperationKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new System.ArgumentException(
                "An operation key must be a non-empty value.",
                nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new System.ArgumentException(
                $"An operation key must be at most {MaxLength} characters.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>The underlying key text.</summary>
    public string Value { get; }

    /// <summary>Returns the underlying key text.</summary>
    public override string ToString() => Value;
}
