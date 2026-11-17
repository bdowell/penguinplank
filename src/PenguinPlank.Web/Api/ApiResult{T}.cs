namespace PenguinPlank.Web.Api;

/// <summary>
/// The outcome of an API call that returns a body: a value or a typed <see cref="ApiError"/>.
/// </summary>
/// <typeparam name="T">The response DTO type.</typeparam>
public sealed record ApiResult<T>
{
    internal ApiResult(T? value, ApiError? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value indicating whether the call succeeded.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Gets the response value on success; otherwise the default value.</summary>
    public T? Value { get; }

    /// <summary>Gets the failure, or <see langword="null"/> on success.</summary>
    public ApiError? Error { get; }
}
