namespace PenguinPlank.Domain.Common;

/// <summary>
/// The outcome of an operation that either succeeds carrying a <typeparamref name="T"/>
/// value or fails with a stable <see cref="BusinessError"/>.
/// </summary>
/// <typeparam name="T">The type of the value produced on success.</typeparam>
/// <remarks>
/// <para>
/// Like <see cref="Result"/>, this type models <em>expected</em> business failures
/// without throwing (coding-standards §6) and is immutable. Instances are created
/// through the non-generic <see cref="Result"/> factory methods
/// (<see cref="Result.Success{T}(T)"/> and
/// <see cref="Result.Failure{T}(ErrorCode, string)"/>) so a single type hosts the
/// factories (avoiding static members on this generic type). A failure carries
/// only a stable code and a caller-safe message — never an exception, stack trace,
/// secret, or value.
/// </para>
/// </remarks>
public sealed class Result<T>
{
    private readonly T _value;
    private readonly BusinessError _error;

    internal Result(bool isSuccess, T value, BusinessError error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary><see langword="true"/> when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary><see langword="true"/> when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The produced value. Only valid when <see cref="IsSuccess"/> is
    /// <see langword="true"/>.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when accessed on a failed result.
    /// </exception>
    public T Value =>
        IsSuccess
            ? _value
            : throw new System.InvalidOperationException(
                "A failed result has no value.");

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
}
