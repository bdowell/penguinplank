namespace PenguinPlank.Domain.Common;

/// <summary>
/// The outcome of an operation that either succeeds with no value or fails with a
/// stable <see cref="BusinessError"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Result"/> models <em>expected</em> business failures without
/// throwing (coding-standards §6). It is immutable: construct it only through the
/// <see cref="Success()"/> and <see cref="Failure(ErrorCode, string)"/> factory
/// methods. A failure carries only a stable code and a caller-safe message —
/// never an exception, stack trace, or secret.
/// </para>
/// <para>
/// The generic value-carrying variant <see cref="Result{T}"/> is also created
/// from this type's <see cref="Success{T}(T)"/> and
/// <see cref="Failure{T}(ErrorCode, string)"/> factories, so there is a single
/// discoverable entry point for producing either outcome.
/// </para>
/// </remarks>
public sealed class Result
{
    private readonly BusinessError _error;

    private Result(bool isSuccess, BusinessError error)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    /// <summary><see langword="true"/> when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary><see langword="true"/> when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The failure error. Only valid when <see cref="IsFailure"/> is
    /// <see langword="true"/>.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when accessed on a successful result.
    /// </exception>
    public BusinessError Error =>
        IsFailure
            ? _error
            : throw new System.InvalidOperationException(
                "A successful result has no error.");

    /// <summary>Creates a successful result.</summary>
    public static Result Success() => new(true, default);

    /// <summary>Creates a failed result from an existing <see cref="BusinessError"/>.</summary>
    /// <param name="error">The business error describing the failure.</param>
    public static Result Failure(BusinessError error) => new(false, error);

    /// <summary>Creates a failed result from a stable code and caller-safe message.</summary>
    /// <param name="code">The stable business error code. Must not be <see cref="ErrorCode.None"/>.</param>
    /// <param name="message">A non-empty, caller-safe description of the failure.</param>
    public static Result Failure(ErrorCode code, string message) =>
        new(false, new BusinessError(code, message));

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    /// <typeparam name="T">The type of the value produced on success.</typeparam>
    /// <param name="value">The value produced by the operation.</param>
    public static Result<T> Success<T>(T value) => new(true, value, default);

    /// <summary>Creates a failed value-carrying result from an existing <see cref="BusinessError"/>.</summary>
    /// <typeparam name="T">The type the operation would have produced on success.</typeparam>
    /// <param name="error">The business error describing the failure.</param>
    public static Result<T> Failure<T>(BusinessError error) => new(false, default!, error);

    /// <summary>Creates a failed value-carrying result from a stable code and caller-safe message.</summary>
    /// <typeparam name="T">The type the operation would have produced on success.</typeparam>
    /// <param name="code">The stable business error code. Must not be <see cref="ErrorCode.None"/>.</param>
    /// <param name="message">A non-empty, caller-safe description of the failure.</param>
    public static Result<T> Failure<T>(ErrorCode code, string message) =>
        new(false, default!, new BusinessError(code, message));
}
