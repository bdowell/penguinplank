using PenguinPlank.Domain.Common;

namespace PenguinPlank.Api.Errors;

/// <summary>
/// The single, stable mapping from a Domain <see cref="ErrorCode"/> to its HTTP status code
/// and its machine-readable error-code string surfaced in a ProblemDetails response
/// (requirement A1 §4.8).
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>pure</b> lookup: it takes an ordinary <see cref="ErrorCode"/> value and returns
/// an <see cref="ErrorCodeHttpMapping"/> with no dependency on <c>HttpContext</c>, configuration,
/// a clock, a database, or any other ambient state (coding-standards §1, §2). Both the exception /
/// Result translation at the API boundary and the Property 24 test exercise this same function, so
/// the HTTP contract a client observes is exactly what a unit test can assert without starting the
/// application.
/// </para>
/// <para>
/// The <see cref="Code"/> string is a <b>stable</b> part of the public API error contract. It is
/// the invariant name of the originating <see cref="ErrorCode"/> member, so a client can branch on
/// it reliably. Never rename an existing code string and never reuse a retired one; add a new
/// mapping when a new <see cref="ErrorCode"/> member is introduced.
/// </para>
/// <para>
/// Every <see cref="ErrorCode"/> member (except the success sentinel <see cref="ErrorCode.None"/>)
/// has an explicit mapping here with no gaps. An unmapped code is treated as a programming mistake
/// and surfaces as an <see cref="System.ArgumentOutOfRangeException"/> rather than silently
/// degrading to a wrong status.
/// </para>
/// </remarks>
/// <param name="StatusCode">The HTTP status code the error maps to.</param>
/// <param name="Code">
/// The stable, machine-readable error-code string placed in the ProblemDetails <c>errorCode</c>
/// extension member.
/// </param>
public readonly record struct ErrorCodeHttpMapping(int StatusCode, string Code)
{
    /// <summary>
    /// The stable error-code string reported for an unexpected (non-business) failure that
    /// surfaces as a generic <c>500</c>. It carries no internal detail (requirement A6 §8.5).
    /// </summary>
    public const string UnexpectedCode = "Unexpected";

    /// <summary>
    /// Maps a business <see cref="ErrorCode"/> to its HTTP status and stable error-code string.
    /// </summary>
    /// <param name="errorCode">The expected-business-failure code carried by a failed result.</param>
    /// <returns>The HTTP status and stable code string for <paramref name="errorCode"/>.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when <paramref name="errorCode"/> is <see cref="ErrorCode.None"/> (not a failure) or
    /// a value with no mapping. Both indicate a programming mistake, not an expected business
    /// outcome (coding-standards §6).
    /// </exception>
    public static ErrorCodeHttpMapping Map(ErrorCode errorCode)
    {
        return errorCode switch
        {
            // 400 — the request failed input validation in a way not covered by a more specific
            // code.
            ErrorCode.Validation => new ErrorCodeHttpMapping(
                StatusCodes.Status400BadRequest, nameof(ErrorCode.Validation)),
            ErrorCode.InvalidDimension => new ErrorCodeHttpMapping(
                StatusCodes.Status400BadRequest, nameof(ErrorCode.InvalidDimension)),
            ErrorCode.InvalidProportion => new ErrorCodeHttpMapping(
                StatusCodes.Status400BadRequest, nameof(ErrorCode.InvalidProportion)),

            // 403 — the authenticated actor is not permitted to perform the operation. (401 for an
            // unauthenticated caller is produced by the authentication challenge, not from a
            // business error code.)
            ErrorCode.Forbidden => new ErrorCodeHttpMapping(
                StatusCodes.Status403Forbidden, nameof(ErrorCode.Forbidden)),

            // 409 — a business conflict with existing state (duplicate keys, idempotency-key reuse
            // with a changed payload, an external-mapping collision, or a new transaction against an
            // archived record).
            ErrorCode.DuplicateSku => new ErrorCodeHttpMapping(
                StatusCodes.Status409Conflict, nameof(ErrorCode.DuplicateSku)),
            ErrorCode.DuplicatePieceCode => new ErrorCodeHttpMapping(
                StatusCodes.Status409Conflict, nameof(ErrorCode.DuplicatePieceCode)),
            ErrorCode.ArchivedRecord => new ErrorCodeHttpMapping(
                StatusCodes.Status409Conflict, nameof(ErrorCode.ArchivedRecord)),
            ErrorCode.TrackingModeLocked => new ErrorCodeHttpMapping(
                StatusCodes.Status409Conflict, nameof(ErrorCode.TrackingModeLocked)),
            ErrorCode.IdempotencyConflict => new ErrorCodeHttpMapping(
                StatusCodes.Status409Conflict, nameof(ErrorCode.IdempotencyConflict)),
            ErrorCode.MappingConflict => new ErrorCodeHttpMapping(
                StatusCodes.Status409Conflict, nameof(ErrorCode.MappingConflict)),

            // 412 — an edit targeted a stale Row_Version via If-Match; the caller must refresh and
            // retry rather than silently overwrite newer state.
            ErrorCode.StaleVersion => new ErrorCodeHttpMapping(
                StatusCodes.Status412PreconditionFailed, nameof(ErrorCode.StaleVersion)),

            // 413 — a media upload exceeded an allowed bound (or otherwise failed upload
            // validation).
            ErrorCode.UploadRejected => new ErrorCodeHttpMapping(
                StatusCodes.Status413PayloadTooLarge, nameof(ErrorCode.UploadRejected)),

            // ErrorCode.None is the success sentinel and must never reach this mapper; any other
            // value is an unmapped code. Both are programming mistakes.
            _ => throw new System.ArgumentOutOfRangeException(
                nameof(errorCode),
                errorCode,
                "No HTTP mapping is defined for this error code."),
        };
    }
}
