namespace PenguinPlank.Web.Api;

/// <summary>
/// A typed API failure mapped from an RFC 7807 ProblemDetails body or from a transport failure.
/// </summary>
/// <param name="Kind">The category of failure.</param>
/// <param name="StatusCode">The HTTP status code, or <see langword="null"/> when no response arrived.</param>
/// <param name="ErrorCode">The server's stable machine-readable <c>errorCode</c>, when supplied.</param>
/// <param name="Title">A short caller-safe title.</param>
/// <param name="Detail">A caller-safe description suitable for display, when supplied.</param>
/// <param name="CorrelationId">The per-request correlation id the user can quote, when supplied.</param>
public sealed record ApiError(
    ApiErrorKind Kind,
    int? StatusCode,
    string? ErrorCode,
    string Title,
    string? Detail,
    string? CorrelationId)
{
    /// <summary>Gets a value indicating whether the edit was rejected because the record changed.</summary>
    public bool IsStaleVersion => Kind == ApiErrorKind.StaleVersion;

    /// <summary>Gets a value indicating whether the server could not be reached.</summary>
    public bool IsConnectivityLoss => Kind == ApiErrorKind.Connectivity;
}
