namespace PenguinPlank.Application.Integrations;

/// <summary>
/// An inbound provider notification to be durably recorded before it is acknowledged.
/// </summary>
/// <param name="ConnectionId">The owning integration connection.</param>
/// <param name="DeliveryKey">The provider delivery key used for deduplication.</param>
/// <param name="Payload">A reference to the stored notification payload.</param>
/// <param name="SignatureState">The outcome of validating the provider's signature or authentication (at most 64 characters).</param>
public sealed record InboundDelivery(
    System.Guid ConnectionId,
    DeliveryKey DeliveryKey,
    PayloadRef Payload,
    string SignatureState);
