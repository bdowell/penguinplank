using PenguinPlank.Application.Abstractions;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// Edits the single-row business settings under optimistic concurrency (requirement A2 §5.3; A4
/// §6.5, §6.6, §6.7).
/// </summary>
/// <remarks>
/// <para>
/// The use case validates required inputs, applies the update through
/// <see cref="IBusinessSettingsStore"/> honoring the client's <c>If-Match</c> token, and stamps the
/// modification instant from the injected clock (coding-standards §1, §2). A stale <c>If-Match</c>
/// returns <see cref="ErrorCode.StaleVersion"/> and nothing is persisted; an absent settings record
/// or malformed inputs return <see cref="ErrorCode.Validation"/>.
/// </para>
/// <para>
/// <b>Auditing (requirement A4 §6.7).</b> The settings edit is audited automatically by the audit
/// <c>SaveChanges</c> interceptor: <c>BusinessSettings</c> is a business entity (not an excluded
/// Identity/idempotency/audit type), so when the store's <c>SaveChanges</c> commits the update, an
/// <see cref="PenguinPlank.Domain.Auditing.AuditEntry"/> is written in the <em>same</em>
/// transaction. The use case therefore does not record a second, redundant entry; it relies on the
/// one-and-only transactional audit the interceptor produces.
/// </para>
/// <para>
/// The settings endpoint is gated Owner-only at the API boundary; this use case carries no
/// authorization decision of its own (coding-standards §3).
/// </para>
/// </remarks>
public sealed class UpdateBusinessSettingsUseCase
{
    private readonly IBusinessSettingsStore _settingsStore;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the use case with its injected boundaries.</summary>
    /// <param name="settingsStore">The business-settings persistence boundary.</param>
    /// <param name="timeProvider">The injected clock supplying the modification instant.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public UpdateBusinessSettingsUseCase(
        IBusinessSettingsStore settingsStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _settingsStore = settingsStore;
        _timeProvider = timeProvider;
    }

    /// <summary>Executes the settings edit.</summary>
    /// <param name="request">The decided update, carrying the client's <c>If-Match</c> token.</param>
    /// <param name="actor">The authenticated Owner performing the edit.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A success carrying the updated <see cref="BusinessSettingsView"/>; a
    /// <see cref="ErrorCode.Validation"/> failure for a missing record or malformed inputs; or a
    /// <see cref="ErrorCode.StaleVersion"/> failure for a stale edit (nothing is persisted or
    /// audited on failure).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<Result<BusinessSettingsView>> ExecuteAsync(
        UpdateBusinessSettingsRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        if (string.IsNullOrWhiteSpace(request.Timezone))
        {
            return Result.Failure<BusinessSettingsView>(ErrorCode.Validation, "Business settings require a timezone.");
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            return Result.Failure<BusinessSettingsView>(ErrorCode.Validation, "Business settings require a currency.");
        }

        if (string.IsNullOrWhiteSpace(request.DefaultDimensionUnit))
        {
            return Result.Failure<BusinessSettingsView>(ErrorCode.Validation, "Business settings require a default dimension unit.");
        }

        if (request.DefaultLaborRate < 0 || request.DefaultOverheadRate < 0)
        {
            return Result.Failure<BusinessSettingsView>(ErrorCode.Validation, "Default rates must be non-negative.");
        }

        if (request.ImageSizeLimitBytes <= 0 || request.VideoSizeLimitBytes <= 0)
        {
            return Result.Failure<BusinessSettingsView>(ErrorCode.Validation, "Media size limits must be positive.");
        }

        // The actor is validated above and attributed through the scoped IActorContextAccessor the
        // API boundary set; the audit entry itself is written transactionally by the SaveChanges
        // interceptor when the store commits the BusinessSettings update (requirement A4 §6.7), so
        // no explicit entry is recorded here.
        DateTimeOffset updatedAtUtc = _timeProvider.GetUtcNow();

        return await _settingsStore
            .UpdateAsync(request, updatedAtUtc, cancellationToken)
            .ConfigureAwait(false);
    }
}
