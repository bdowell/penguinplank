namespace PenguinPlank.Domain.Media;

/// <summary>
/// The visibility of a <see cref="MediaAsset"/>, controlling whether its content is eligible for
/// public exposure.
/// </summary>
/// <remarks>
/// Visibility is <b>metadata only</b>: being <see cref="PublicApproved"/> marks an asset as
/// eligible for a public projection but never, by itself, grants anonymous access to the backing
/// file. Every download is authorized at the API boundary (requirement 6.11 / A4). New assets
/// start <see cref="Private"/>.
/// </remarks>
public enum MediaVisibility
{
    /// <summary>The default. Private media is never exposed on a public surface.</summary>
    Private = 0,

    /// <summary>
    /// Media explicitly approved for public exposure. Eligibility for a public projection only;
    /// downloads remain authorized and are never anonymously accessible.
    /// </summary>
    PublicApproved = 1,
}
