using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Integrations;

/// <summary>
/// An opaque pointer to an external credential secret held in the deployment secret store (for
/// example <c>keyvault://penguinplank/shopify-main</c>). It is never the secret value
/// (requirement R11 / 3.2).
/// </summary>
/// <remarks>
/// The type validates only the shape of a reference: non-empty, bounded in length, and free of
/// whitespace and control characters. It cannot tell a reference from a secret, so callers must
/// pass the secret store's locator, never the secret. <see cref="ToString"/> is intentionally
/// not overridden to reveal more than the locator, and nothing here logs it.
/// </remarks>
public sealed record CredentialReference
{
    /// <summary>The maximum reference length, matching the persisted column width.</summary>
    public const int MaximumLength = 400;

    private CredentialReference(string value)
    {
        Value = value;
    }

    /// <summary>The secret-store locator.</summary>
    public string Value { get; }

    /// <summary>Validates and creates a reference.</summary>
    /// <param name="value">The secret-store locator.</param>
    /// <returns>The reference, or a validation failure when the value is empty, too long, or malformed.</returns>
    public static Result<CredentialReference> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<CredentialReference>(
                ErrorCode.Validation,
                "A credential reference is required.");
        }

        if (value.Length > MaximumLength)
        {
            return Result.Failure<CredentialReference>(
                ErrorCode.Validation,
                $"A credential reference must be at most {MaximumLength} characters.");
        }

        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                return Result.Failure<CredentialReference>(
                    ErrorCode.Validation,
                    "A credential reference must not contain whitespace or control characters.");
            }
        }

        return Result.Success(new CredentialReference(value));
    }
}
