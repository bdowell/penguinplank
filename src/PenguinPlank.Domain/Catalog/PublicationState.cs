namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// The lifecycle state controlling whether a catalog record's public-facing content is
/// exposed externally.
/// </summary>
/// <remarks>
/// <para>
/// Newly created Product, ProductVariant, and ProductPiece content always starts in
/// <see cref="Draft"/> and is only exposed after an explicit transition to
/// <see cref="PublicApproved"/> (requirements 1.12, 2.6, 2.7; design invariant 11). The
/// default is enforced both as a POCO default on the owning entity and as an EF
/// <c>HasDefaultValue</c> at the database level so a record inserted outside a use case
/// cannot start life publicly visible.
/// </para>
/// <para>
/// Phase A models the two states the acceptance criteria require (draft and public-approved).
/// Additional intermediate states (for example a review or archived-publication state) are
/// an additive change to this enum in a later phase and do not alter the default.
/// </para>
/// </remarks>
public enum PublicationState
{
    /// <summary>
    /// The default state for newly created content. Draft content is never exposed to a
    /// public or customer-facing surface.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Content that has been explicitly reviewed and approved for public exposure. Only
    /// content in this state is eligible for a public projection, and even then only the
    /// allowlisted public-ready fields are serialized (never internal fields).
    /// </summary>
    PublicApproved = 1,
}
