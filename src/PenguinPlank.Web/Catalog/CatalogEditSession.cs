using PenguinPlank.Web.Api;

namespace PenguinPlank.Web.Catalog;

/// <summary>Sends one edit request to the API.</summary>
/// <typeparam name="TRequest">The request contract.</typeparam>
/// <param name="request">The edit to send.</param>
/// <param name="eTag">The version token of the record being edited.</param>
/// <param name="idempotencyKey">The key to send with the request.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>The outcome of the call.</returns>
public delegate Task<ApiResult> CatalogEditSender<in TRequest>(
    TRequest request,
    string eTag,
    string idempotencyKey,
    CancellationToken cancellationToken);

/// <summary>
/// Tracks one edit of a catalog record: stale-version conflicts, connectivity loss, and
/// idempotency-key reuse (requirements A5 7.5 to 7.8).
/// </summary>
/// <remarks>
/// No edit is ever queued. After a connectivity failure the session keeps the failed request and
/// its idempotency key, and reuses that key only when the user explicitly submits an identical
/// request against the same version. Any other submit gets a new key.
/// </remarks>
/// <typeparam name="TRequest">The request contract; it needs value equality, as a record has.</typeparam>
public sealed class CatalogEditSession<TRequest>
    where TRequest : class
{
    private readonly IIdempotencyKeyGenerator _keyGenerator;
    private PendingEdit? _pending;

    /// <summary>Initializes a new instance of the <see cref="CatalogEditSession{TRequest}"/> class.</summary>
    /// <param name="keyGenerator">Supplies idempotency keys.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="keyGenerator"/> is <see langword="null"/>.</exception>
    public CatalogEditSession(IIdempotencyKeyGenerator keyGenerator)
    {
        ArgumentNullException.ThrowIfNull(keyGenerator);
        _keyGenerator = keyGenerator;
    }

    /// <summary>Gets the current status.</summary>
    public CatalogEditStatus Status { get; private set; } = CatalogEditStatus.Editing;

    /// <summary>Gets the version token of the loaded record, or <see langword="null"/> when a reload is required.</summary>
    public string? ETag { get; private set; }

    /// <summary>Gets the failure from the last submit, when there was one.</summary>
    public ApiError? Error { get; private set; }

    /// <summary>Gets the idempotency key a resubmit would reuse, or <see langword="null"/> when none is held.</summary>
    public string? PendingIdempotencyKey => _pending?.Key;

    /// <summary>Gets a value indicating whether a submit is allowed now.</summary>
    public bool CanSubmit => ETag is not null && Status is not (CatalogEditStatus.Saving or CatalogEditStatus.Conflict);

    /// <summary>Gets a value indicating whether the user must reload the record before editing.</summary>
    public bool RequiresReload => Status == CatalogEditStatus.Conflict || ETag is null;

    /// <summary>Gets a value indicating whether the last edit was not saved.</summary>
    public bool HasUnsavedEdit => Status is CatalogEditStatus.Conflict or CatalogEditStatus.Unsaved or CatalogEditStatus.Rejected;

    /// <summary>Gets the text to show for the current status, or <see langword="null"/> when there is nothing to report.</summary>
    public string? Message => Status switch
    {
        CatalogEditStatus.Saving => "Saving…",
        CatalogEditStatus.Saved => ETag is null ? "Saved, but the latest version could not be loaded. Reload to keep editing." : "Saved.",
        _ => Error is null ? null : CatalogMessages.Describe(Error),
    };

    /// <summary>Starts editing a freshly loaded record, discarding any held failure and key.</summary>
    /// <param name="eTag">The version token of the loaded record.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eTag"/> is null or blank.</exception>
    public void Load(string eTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eTag);
        ETag = eTag;
        Status = CatalogEditStatus.Editing;
        Error = null;
        _pending = null;
    }

    /// <summary>Records that a save succeeded and the record was reloaded at its new version.</summary>
    /// <param name="eTag">The version token after the save.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eTag"/> is null or blank.</exception>
    public void CompleteSave(string eTag)
    {
        Load(eTag);
        Status = CatalogEditStatus.Saved;
    }

    /// <summary>
    /// Records that a save succeeded but the record could not be reloaded, so the version is
    /// unknown and the user must reload before editing again.
    /// </summary>
    /// <param name="reloadError">The failure from the reload.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reloadError"/> is <see langword="null"/>.</exception>
    public void CompleteSaveWithoutReload(ApiError reloadError)
    {
        ArgumentNullException.ThrowIfNull(reloadError);
        ETag = null;
        Status = CatalogEditStatus.Saved;
        Error = reloadError;
        _pending = null;
    }

    /// <summary>Submits an edit.</summary>
    /// <param name="request">The edit to send.</param>
    /// <param name="sender">Sends the request through the API client.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the server confirmed the edit; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public async Task<bool> SubmitAsync(TRequest request, CatalogEditSender<TRequest> sender, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sender);

        if (!CanSubmit || ETag is null)
        {
            return false;
        }

        string eTag = ETag;
        PendingEdit attempt = _pending is not null && _pending.Request.Equals(request) && _pending.ETag == eTag
            ? _pending
            : new PendingEdit(request, eTag, _keyGenerator.NewKey());

        Status = CatalogEditStatus.Saving;
        Error = null;

        ApiResult result;
        try
        {
            result = await sender(request, eTag, attempt.Key, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The outcome is unknown, so keep the key for an explicit resubmit.
            _pending = attempt;
            Status = CatalogEditStatus.Unsaved;
            throw;
        }

        if (result.IsSuccess)
        {
            _pending = null;
            Status = CatalogEditStatus.Saved;
            return true;
        }

        ApiError error = result.Error ?? new ApiError(ApiErrorKind.Unexpected, null, null, "Unexpected result", null, null);
        Error = error;
        Status = error.Kind switch
        {
            ApiErrorKind.StaleVersion => CatalogEditStatus.Conflict,
            ApiErrorKind.Connectivity => CatalogEditStatus.Unsaved,
            _ => CatalogEditStatus.Rejected,
        };

        // After a lost connection or a server failure the request may have been applied, so keep
        // its key for an explicit resubmit. Any other answer means it was not applied.
        _pending = error.Kind is ApiErrorKind.Connectivity or ApiErrorKind.Server ? attempt : null;
        return false;
    }

    private sealed record PendingEdit(TRequest Request, string ETag, string Key);
}
