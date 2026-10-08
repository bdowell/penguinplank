using PenguinPlank.Domain.Media;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// Pure policy that produces the public-safe projection of a <see cref="Product"/>: the explicit
/// allowlist of Public_Ready_Field values an anonymous or customer-facing catalog consumer may
/// see.
/// </summary>
/// <remarks>
/// <para>
/// This policy enforces two guarantees required by R01 1.11 and R10 2.10 / 2.11 (design
/// Property 9), both <b>structurally</b> rather than by trusting serialization settings:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Exclude internal fields.</b> The output type <see cref="PublicProductView"/> (and its
/// nested <see cref="PublicMediaView"/>) declares only allowlisted public members. No
/// Internal_Field — internal notes, cost, margin, production notes, contact information,
/// wholesale notes — nor the publication state or concurrency token exists on the shape, so the
/// policy cannot copy one across even in principle. No internal entity is serialized wholesale.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Public-approved only.</b> A view is produced only when the product's
/// <see cref="Product.PublicationState"/> is <see cref="PublicationState.PublicApproved"/>. A
/// <see cref="PublicationState.Draft"/> product is not publicly visible, so the policy returns
/// <see langword="null"/> and no draft content (not even a partial view) ever reaches a public
/// projection.
/// </description>
/// </item>
/// </list>
/// <para>
/// This public projection is a different concern from the role-based variant projector
/// (<c>CatalogVariantProjector</c>), which strips Owner-only financial fields for a Staff actor:
/// that one answers "what may an authenticated role see", while this one answers "what may an
/// unauthenticated public consumer see" and therefore excludes internal content entirely and
/// surfaces public-approved content only.
/// </para>
/// <para>
/// The policy is a pure static function over an explicit product and media collection — no I/O,
/// clock, configuration, or ambient state — so it is directly unit-testable without application
/// startup (coding-standards §1, §3).
/// </para>
/// </remarks>
public static class PublicProjectionPolicy
{
    /// <summary>
    /// Produces the public-safe projection of a product, or <see langword="null"/> when the
    /// product is not publicly visible.
    /// </summary>
    /// <param name="product">The product's rich internal representation.</param>
    /// <param name="approvedMedia">
    /// The media associated with the product. Only entries whose
    /// <see cref="MediaAsset.Visibility"/> is <see cref="MediaVisibility.PublicApproved"/> are
    /// projected; any other entry is excluded. A <see langword="null"/> collection is treated as
    /// no media.
    /// </param>
    /// <returns>
    /// A <see cref="PublicProductView"/> carrying only allowlisted public fields when
    /// <paramref name="product"/> is in <see cref="PublicationState.PublicApproved"/>; otherwise
    /// <see langword="null"/>, indicating the product is not publicly visible (for example, while
    /// it is in <see cref="PublicationState.Draft"/>).
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="product"/> is <see langword="null"/>.
    /// </exception>
    public static PublicProductView? ToPublicView(
        Product product,
        IReadOnlyCollection<MediaAsset>? approvedMedia)
    {
        System.ArgumentNullException.ThrowIfNull(product);

        if (product.PublicationState != PublicationState.PublicApproved)
        {
            return null;
        }

        IReadOnlyList<PublicMediaView> media = ProjectApprovedMedia(approvedMedia);

        return new PublicProductView(
            product.Name,
            product.Category,
            product.PublicDescription,
            media);
    }

    /// <summary>
    /// Projects only the public-approved media into the public-safe media shape, ordered by sort
    /// order, excluding any asset that is not approved for public exposure.
    /// </summary>
    /// <param name="approvedMedia">The candidate media, which may include non-approved entries.</param>
    /// <returns>The public-safe media views; empty when none qualify.</returns>
    private static IReadOnlyList<PublicMediaView> ProjectApprovedMedia(
        IReadOnlyCollection<MediaAsset>? approvedMedia)
    {
        if (approvedMedia is null || approvedMedia.Count == 0)
        {
            return System.Array.Empty<PublicMediaView>();
        }

        return approvedMedia
            .Where(static asset => asset.Visibility == MediaVisibility.PublicApproved)
            .OrderBy(static asset => asset.SortOrder)
            .Select(static asset => new PublicMediaView(
                asset.StorageKey,
                asset.Caption,
                asset.Role,
                asset.SortOrder))
            .ToList();
    }
}
