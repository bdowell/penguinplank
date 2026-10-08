using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Media;

/// <summary>
/// The storage boundary for media binary content, held <b>outside the relational database and
/// outside the web root</b> (requirements 6.8–6.11 / A4).
/// </summary>
/// <remarks>
/// <para>
/// This is a narrow, responsibility-named boundary interface defined in Application and implemented
/// in Infrastructure (coding-standards §2, §3). Phase A ships a local filesystem adapter; a cloud
/// object-store adapter can replace it later without touching callers (requirement 6.8). The
/// contract is deliberately small — save, open, delete — so a use case can be tested against a
/// controllable substitute.
/// </para>
/// <para>
/// Responsibilities and guarantees:
/// </para>
/// <list type="bullet">
///   <item><description><b>Validate on save.</b> <see cref="SaveAsync"/> applies the pure
///   <c>MediaUploadValidationPolicy</c> (allowlist + magic-byte signature + reject
///   executable/HTML + size bound) and returns a <see cref="Result{T}"/> failure with
///   <see cref="ErrorCode.UploadRejected"/> when the upload is rejected — no file is written in
///   that case (requirements 6.9, 6.10).</description></item>
///   <item><description><b>Randomized keys.</b> A successful save returns a randomized, unique
///   <see cref="StorageKey"/> that is never derived from the client filename (requirement
///   6.9).</description></item>
///   <item><description><b>Authorized access only.</b> <see cref="OpenAsync"/> opens a stream for a
///   request the caller has already authorized at the API boundary. The store does not inspect
///   media visibility and never grants anonymous access; public-approved is a publishing state, not
///   an access grant (requirement 6.11).</description></item>
/// </list>
/// <para>
/// Every method takes and propagates a <see cref="CancellationToken"/>. Expected business failures
/// (a rejected upload, a missing key) are returned as a typed <see cref="Result"/>; unexpected I/O
/// faults throw (coding-standards §6).
/// </para>
/// </remarks>
public interface IFileStore
{
    /// <summary>
    /// Validates and persists a media upload, returning the randomized storage key and the facts
    /// the metadata record needs.
    /// </summary>
    /// <param name="upload">The upload to validate and store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success carrying the <see cref="StoredMedia"/> descriptor, or a failure with
    /// <see cref="ErrorCode.UploadRejected"/> when validation rejects the upload. No file is written
    /// on failure.
    /// </returns>
    Task<Result<StoredMedia>> SaveAsync(MediaUpload upload, CancellationToken cancellationToken);

    /// <summary>
    /// Opens the binary content for an <b>already-authorized</b> download.
    /// </summary>
    /// <param name="key">The storage key of the file to open.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success carrying a readable <see cref="MediaContent"/> the caller must dispose, or a failure
    /// when no file exists for the key. Authorization is enforced by the caller before invoking this
    /// method; the store never grants anonymous access (requirement 6.11).
    /// </returns>
    Task<Result<MediaContent>> OpenAsync(StorageKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the binary content for the given key.
    /// </summary>
    /// <param name="key">The storage key of the file to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success when the file was deleted or already absent (idempotent); the operation does not
    /// fail merely because the file was already gone.
    /// </returns>
    Task<Result> DeleteAsync(StorageKey key, CancellationToken cancellationToken);
}
