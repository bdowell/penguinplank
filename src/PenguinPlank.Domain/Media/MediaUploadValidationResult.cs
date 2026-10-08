namespace PenguinPlank.Domain.Media;

/// <summary>
/// The specific reason a media upload was rejected (requirement 6.9 / 6.10). Every rejection maps
/// to <c>ErrorCode.UploadRejected</c> at the boundary; this value records the precise cause for a
/// caller-safe message and for tests.
/// </summary>
public enum MediaUploadRejectionReason
{
    /// <summary>Not a rejection; the upload was accepted.</summary>
    None = 0,

    /// <summary>The claimed content type is not on the allowlist.</summary>
    DisallowedContentType = 1,

    /// <summary>
    /// The file's leading bytes do not match the magic-byte signature of the claimed content type.
    /// </summary>
    SignatureMismatch = 2,

    /// <summary>
    /// The file's leading bytes match an executable or HTML/script signature, which is rejected
    /// regardless of the claimed content type.
    /// </summary>
    DangerousContent = 3,

    /// <summary>The upload size exceeds the bound for its category (20 MB image / 200 MB video).</summary>
    SizeExceeded = 4,

    /// <summary>The upload carries no content, or the declared size is not positive.</summary>
    EmptyContent = 5,
}

/// <summary>
/// The outcome of the pure media upload validation policy: acceptance (with the resolved category)
/// or a rejection carrying a specific reason and a caller-safe message (requirement 6.9 / 6.10).
/// </summary>
/// <remarks>
/// <para>
/// This is a pure value returned by <see cref="MediaUploadValidationPolicy.Validate"/>. It is the
/// unit the property test (task 6.10, Property 17) asserts against, so it exposes the exact reason
/// a decision was reached without requiring the application or a file store to run. The caller (the
/// media upload use case) translates a rejection into a <c>Result</c> failure with
/// <c>ErrorCode.UploadRejected</c>.
/// </para>
/// </remarks>
public sealed class MediaUploadValidationResult
{
    private readonly MediaCategory _category;

    private MediaUploadValidationResult(bool isAccepted, MediaCategory category, MediaUploadRejectionReason reason, string message)
    {
        IsAccepted = isAccepted;
        _category = category;
        Reason = reason;
        Message = message;
    }

    /// <summary><see langword="true"/> when the upload passed every validation check.</summary>
    public bool IsAccepted { get; }

    /// <summary><see langword="true"/> when the upload was rejected.</summary>
    public bool IsRejected => !IsAccepted;

    /// <summary>The specific rejection reason, or <see cref="MediaUploadRejectionReason.None"/> when accepted.</summary>
    public MediaUploadRejectionReason Reason { get; }

    /// <summary>A caller-safe description of the decision. Carries no secret or stack trace.</summary>
    public string Message { get; }

    /// <summary>
    /// The resolved media category. Only valid when <see cref="IsAccepted"/> is
    /// <see langword="true"/>.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">Thrown when accessed on a rejection.</exception>
    public MediaCategory Category =>
        IsAccepted
            ? _category
            : throw new System.InvalidOperationException("A rejected upload has no resolved category.");

    /// <summary>Creates an acceptance for the given resolved <paramref name="category"/>.</summary>
    /// <param name="category">The category resolved from the allowlisted content type.</param>
    public static MediaUploadValidationResult Accept(MediaCategory category) =>
        new(isAccepted: true, category, MediaUploadRejectionReason.None, "The upload is valid.");

    /// <summary>Creates a rejection with a specific reason and caller-safe message.</summary>
    /// <param name="reason">The precise cause; must not be <see cref="MediaUploadRejectionReason.None"/>.</param>
    /// <param name="message">A non-empty, caller-safe description of the rejection.</param>
    /// <exception cref="System.ArgumentException">
    /// Thrown when <paramref name="reason"/> is <see cref="MediaUploadRejectionReason.None"/> or
    /// <paramref name="message"/> is blank.
    /// </exception>
    public static MediaUploadValidationResult Reject(MediaUploadRejectionReason reason, string message)
    {
        if (reason == MediaUploadRejectionReason.None)
        {
            throw new System.ArgumentException("A rejection cannot use reason None.", nameof(reason));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new System.ArgumentException("A rejection requires a non-empty, caller-safe message.", nameof(message));
        }

        return new MediaUploadValidationResult(isAccepted: false, default, reason, message);
    }
}
