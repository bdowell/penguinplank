using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.IdentityAdministration.Administration;

/// <summary>
/// The narrow persistence boundary for the single-row business settings (requirement A2 §5.3).
/// </summary>
/// <remarks>
/// <para>
/// A focused, responsibility-named interface defined in Application and implemented in
/// Infrastructure over EF Core (coding-standards §2, §3). It reads the single settings record as an
/// Application <see cref="BusinessSettingsView"/> and applies a decided update under optimistic
/// concurrency; no EF entity, <c>DbContext</c>, or <c>IQueryable</c> crosses the boundary. Every
/// method takes and propagates a <see cref="CancellationToken"/>.
/// </para>
/// <para>
/// The store performs no authorization decision — the settings endpoints are gated Owner-only at
/// the API boundary. The update honors the client's <c>If-Match</c> token so a stale write fails
/// with <see cref="ErrorCode.StaleVersion"/> rather than overwriting newer settings (requirement A4
/// §6.6).
/// </para>
/// </remarks>
public interface IBusinessSettingsStore
{
    /// <summary>
    /// Reads the single business-settings record.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The settings view, or <see langword="null"/> when the record has not been seeded.</returns>
    Task<BusinessSettingsView?> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies a decided update to the business settings under optimistic concurrency, stamping the
    /// supplied modification instant.
    /// </summary>
    /// <param name="request">The decided update, carrying the client's <c>If-Match</c> token.</param>
    /// <param name="updatedAtUtc">The modification instant supplied from an injected clock.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A success carrying the updated view; a <see cref="ErrorCode.Validation"/> failure when no
    /// settings record exists or the <c>If-Match</c> token is malformed; or a
    /// <see cref="ErrorCode.StaleVersion"/> failure when the stored version has advanced.
    /// </returns>
    Task<Result<BusinessSettingsView>> UpdateAsync(
        UpdateBusinessSettingsRequest request,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
}
