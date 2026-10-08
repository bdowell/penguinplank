namespace PenguinPlank.Domain.Common;

/// <summary>
/// Stable catalog of Phase A business error codes.
/// </summary>
/// <remarks>
/// <para>
/// Each member identifies an expected business failure. Values are carried by
/// <see cref="Result"/> and <see cref="Result{T}"/> and are later mapped to RFC
/// ProblemDetails responses at the API boundary (requirement A1 / 4.8). Because
/// a code becomes part of the public API error contract, the explicit numeric
/// values assigned here are <b>stable</b>: never renumber an existing member and
/// never reuse a retired value. Add new members with the next unused value.
/// </para>
/// <para>
/// These codes describe <em>expected</em> outcomes only. Unexpected failures and
/// violated programming assumptions are signalled with exceptions, not with a
/// code (coding-standards §6).
/// </para>
/// </remarks>
public enum ErrorCode
{
    /// <summary>No error. Reserved for the success state; never carried by a failure.</summary>
    None = 0,

    /// <summary>
    /// A requested value failed input validation (for example a missing required
    /// field or a malformed value) in a way not covered by a more specific code.
    /// </summary>
    Validation = 1,

    /// <summary>
    /// A ProductVariant SKU collides with an existing ProductVariant SKU
    /// (requirement R01 / 1.2). Maps to a conflict response.
    /// </summary>
    DuplicateSku = 2,

    /// <summary>
    /// A ProductPiece business piece code collides with an existing piece code
    /// (requirement R01 / 1.7). Maps to a conflict response.
    /// </summary>
    DuplicatePieceCode = 3,

    /// <summary>
    /// A new transaction was attempted against archived Product, ProductVariant,
    /// or ProductPiece master data (requirement R01 / 1.8). Existing historical
    /// references remain readable; the new transaction is rejected.
    /// </summary>
    ArchivedRecord = 4,

    /// <summary>
    /// A dimension was negative, exceeded the allowed decimal precision, or was
    /// supplied without an explicit unit (requirement R01 / 1.3, 1.4).
    /// </summary>
    InvalidDimension = 5,

    /// <summary>
    /// A wood-composition proportion fell outside the inclusive 0–100 range
    /// (requirement R01 / 1.5, R10 / 2.2).
    /// </summary>
    InvalidProportion = 6,

    /// <summary>
    /// A ProductVariant tracking-mode change was rejected because stock history
    /// exists and no designed migration applies (requirement R01 / 1.13).
    /// </summary>
    TrackingModeLocked = 7,

    /// <summary>
    /// A mutating command reused an existing Idempotency_Key with a changed
    /// payload hash (requirement A4 / 6.3). Maps to a conflict response.
    /// </summary>
    IdempotencyConflict = 8,

    /// <summary>
    /// An edit targeted a stale Row_Version via <c>If-Match</c>
    /// (requirement A4 / 6.6). The edit is refused rather than silently
    /// overwriting newer state; the caller is prompted to refresh and retry.
    /// </summary>
    StaleVersion = 9,

    /// <summary>
    /// A media upload was rejected by validation (for example an unsupported
    /// content type, a signature mismatch, or an exceeded size bound).
    /// </summary>
    UploadRejected = 10,

    /// <summary>
    /// The authenticated actor is not permitted to perform the requested
    /// operation. Maps to a forbidden response.
    /// </summary>
    Forbidden = 11,

    /// <summary>
    /// An external-platform entity mapping conflicts with an existing mapping,
    /// for example a duplicate platform/account/external-id association
    /// (requirement R11 / 3.4).
    /// </summary>
    MappingConflict = 12,
}
