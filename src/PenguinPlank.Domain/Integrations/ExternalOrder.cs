using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// An order captured from an external platform: its origin and mirrored identifiers, payment and
/// fulfillment state, and posting status.
/// </summary>
/// <remarks>
/// <para>
/// The external-order schema is modeled and backed by stores in Phase A, but the posting/sale
/// logic that turns a captured order into an internal sale is designed-only (design entity
/// catalog). This entity records the order's origin platform/account, the mirrored external
/// identifier, its payment and fulfillment state, and a posting status; normalized lines live in
/// <see cref="ExternalOrderLine"/> and stock reservations in <see cref="OrderReservation"/>.
/// </para>
/// <para>
/// It derives from <see cref="Entity"/>: a captured order is a durable record whose state advances
/// as it is reconciled, and must never be cascade-deleted (the model-wide restrict default
/// applies to its lines and reservations).
/// </para>
/// </remarks>
public class ExternalOrder : Entity
{
    /// <summary>The owning <see cref="IntegrationConnection"/> the order was imported through.</summary>
    public Guid ConnectionId { get; set; }

    /// <summary>The platform the order originated on (for example, "Shopify").</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>The account or shop the order belongs to.</summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>The order's mirrored identifier on the external platform.</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>The order's payment state as reported by the platform.</summary>
    public string PaymentState { get; set; } = string.Empty;

    /// <summary>The order's fulfillment state as reported by the platform.</summary>
    public string FulfillmentState { get; set; } = string.Empty;

    /// <summary>
    /// The internal posting status (whether the captured order has been posted to an internal
    /// sale). Posting logic itself is designed-only in Phase A.
    /// </summary>
    public string PostingStatus { get; set; } = string.Empty;
}
