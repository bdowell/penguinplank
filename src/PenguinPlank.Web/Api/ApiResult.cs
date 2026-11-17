namespace PenguinPlank.Web.Api;

/// <summary>
/// The outcome of an API call that returns no body: success or a typed <see cref="ApiError"/>.
/// Also hosts the factories for <see cref="ApiResult{T}"/>.
/// </summary>
public sealed record ApiResult
{
    private ApiResult(ApiError? error)
    {
        Error = error;
    }

    /// <summary>Gets a value indicating whether the call succeeded.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Gets the failure, or <see langword="null"/> on success.</summary>
    public ApiError? Error { get; }

    /// <summary>Creates a successful result.</summary>
    /// <returns>A successful result.</returns>
    public static ApiResult Success() => new(error: null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The typed failure.</param>
    /// <returns>A failed result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public static ApiResult Failure(ApiError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ApiResult(error);
    }

    /// <summary>Creates a successful result carrying a value.</summary>
    /// <typeparam name="T">The response DTO type.</typeparam>
    /// <param name="value">The response value.</param>
    /// <returns>A successful result.</returns>
    public static ApiResult<T> Success<T>(T value) => new(value, error: null);

    /// <summary>Creates a failed result for a call that returns a value.</summary>
    /// <typeparam name="T">The response DTO type.</typeparam>
    /// <param name="error">The typed failure.</param>
    /// <returns>A failed result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public static ApiResult<T> Failure<T>(ApiError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ApiResult<T>(default, error);
    }
}
