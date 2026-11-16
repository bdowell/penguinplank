namespace PenguinPlank.Application.Integrations;

/// <summary>
/// The durable inbox boundary for inbound provider notifications (requirements 3.6, 3.8).
/// </summary>
/// <remarks>
/// A notification is recorded durably <b>before</b> it is acknowledged to the provider. The
/// delivery key is unique, so a repeated or out-of-order delivery is not recorded twice; the
/// caller then reconciles authoritative state instead of applying the change again.
/// </remarks>
public interface IInboxStore
{
    /// <summary>Durably records a delivery unless its delivery key was already recorded.</summary>
    /// <param name="delivery">The delivery to record.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when the delivery was newly recorded; <see langword="false"/> when
    /// the delivery key already exists (a duplicate), in which case nothing is written.
    /// </returns>
    Task<bool> TryRecordAsync(InboundDelivery delivery, CancellationToken cancellationToken);
}
