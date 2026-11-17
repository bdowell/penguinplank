namespace PenguinPlank.Application.Integrations.Webhooks;

/// <summary>
/// Accepts an inbound provider callback: enforces the size limit, authenticates it, then records
/// it durably in the inbox with deduplication (requirements 3.6, 3.7, 9.1, 9.2, 9.3).
/// </summary>
/// <remarks>
/// A callback that fails any check before the final step writes nothing, neither payload nor inbox
/// row, so a forged callback is never recorded as authoritative. A <see cref="WebhookIntakeOutcome.Recorded"/>
/// or <see cref="WebhookIntakeOutcome.Duplicate"/> outcome means the delivery is durable and may be
/// acknowledged. Storage failures propagate as exceptions so a failed write is never acknowledged.
/// </remarks>
public sealed class ReceiveWebhookUseCase
{
    private readonly IWebhookSignatureVerifier _verifier;
    private readonly IIntegrationConnectionStore _connections;
    private readonly IWebhookPayloadStore _payloads;
    private readonly IInboxStore _inbox;
    private readonly WebhookIntakeOptions _options;

    /// <summary>Creates the use case.</summary>
    /// <param name="verifier">The provider signature verifier.</param>
    /// <param name="connections">The connection store, used to resolve the owning connection.</param>
    /// <param name="payloads">The payload store.</param>
    /// <param name="inbox">The durable inbox.</param>
    /// <param name="options">The intake limits.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when a dependency is null.</exception>
    public ReceiveWebhookUseCase(
        IWebhookSignatureVerifier verifier,
        IIntegrationConnectionStore connections,
        IWebhookPayloadStore payloads,
        IInboxStore inbox,
        WebhookIntakeOptions options)
    {
        System.ArgumentNullException.ThrowIfNull(verifier);
        System.ArgumentNullException.ThrowIfNull(connections);
        System.ArgumentNullException.ThrowIfNull(payloads);
        System.ArgumentNullException.ThrowIfNull(inbox);
        System.ArgumentNullException.ThrowIfNull(options);

        _verifier = verifier;
        _connections = connections;
        _payloads = payloads;
        _inbox = inbox;
        _options = options;
    }

    /// <summary>Handles one callback.</summary>
    /// <param name="callback">The callback.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The intake outcome.</returns>
    public async Task<WebhookIntakeOutcome> ReceiveAsync(
        WebhookCallback callback,
        CancellationToken cancellationToken)
    {
        System.ArgumentNullException.ThrowIfNull(callback);

        if (!WebhookPayloadReference.IsValidProvider(callback.Provider))
        {
            return WebhookIntakeOutcome.UnknownProvider;
        }

        if (callback.Body.Length > _options.MaxPayloadBytes)
        {
            return WebhookIntakeOutcome.PayloadTooLarge;
        }

        WebhookVerification verification = _verifier.Verify(callback);
        if (!verification.IsAuthentic || verification.DeliveryKey is not { } deliveryKey)
        {
            return WebhookIntakeOutcome.Rejected;
        }

        IReadOnlyList<IntegrationConnectionSummary> connections =
            await _connections.ListAsync(cancellationToken).ConfigureAwait(false);
        IntegrationConnectionSummary? connection = connections.FirstOrDefault(
            item => string.Equals(item.Platform, callback.Provider, System.StringComparison.OrdinalIgnoreCase));
        if (connection is null)
        {
            return WebhookIntakeOutcome.UnknownConnection;
        }

        PayloadRef reference = WebhookPayloadReference.For(callback.Provider, deliveryKey);
        await _payloads.SaveIfAbsentAsync(reference, callback.Body, cancellationToken).ConfigureAwait(false);

        var delivery = new InboundDelivery(
            connection.Id,
            deliveryKey,
            reference,
            WebhookVerification.VerifiedSignatureState);
        bool recorded = await _inbox.TryRecordAsync(delivery, cancellationToken).ConfigureAwait(false);

        return recorded ? WebhookIntakeOutcome.Recorded : WebhookIntakeOutcome.Duplicate;
    }
}
