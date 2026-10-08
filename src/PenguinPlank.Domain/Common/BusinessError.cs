namespace PenguinPlank.Domain.Common;

/// <summary>
/// An expected business failure: a stable <see cref="ErrorCode"/> paired with a
/// human-readable message safe to surface to a caller.
/// </summary>
/// <remarks>
/// A <see cref="BusinessError"/> deliberately carries no exception, stack trace,
/// or secret. The message describes the business condition only and must never
/// embed credentials, SQL text, or other sensitive detail (coding-standards §6).
/// </remarks>
public readonly record struct BusinessError
{
    /// <summary>
    /// Creates an error with the given stable code and caller-safe message.
    /// </summary>
    /// <param name="code">The stable business error code. Must not be <see cref="ErrorCode.None"/>.</param>
    /// <param name="message">A non-empty, caller-safe description of the failure.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="code"/> is <see cref="ErrorCode.None"/> or
    /// <paramref name="message"/> is null, empty, or whitespace. A malformed
    /// error is a programming mistake, not an expected business failure, so it
    /// surfaces as an exception.
    /// </exception>
    public BusinessError(ErrorCode code, string message)
    {
        if (code == ErrorCode.None)
        {
            throw new System.ArgumentException(
                "A failure error cannot use ErrorCode.None.",
                nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new System.ArgumentException(
                "An error message must be a non-empty, caller-safe description.",
                nameof(message));
        }

        Code = code;
        Message = message;
    }

    /// <summary>The stable business error code identifying the failure.</summary>
    public ErrorCode Code { get; }

    /// <summary>A caller-safe, human-readable description of the failure.</summary>
    public string Message { get; }
}
