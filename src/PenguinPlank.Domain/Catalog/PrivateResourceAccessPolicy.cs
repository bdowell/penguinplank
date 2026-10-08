using PenguinPlank.Domain.Media;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// Pure policy that decides whether a caller may read a private business resource — a product,
/// contact, media asset, or event — enforcing requirement A5 §7.4 (design Property 12):
/// <b>authentication is the gate, publication/visibility metadata is not</b>.
/// </summary>
/// <remarks>
/// <para>
/// The single decision this policy makes is that an <b>unauthenticated</b> caller is always denied
/// a private resource, and that <see cref="PublicationState.PublicApproved"/> /
/// <see cref="MediaVisibility.PublicApproved"/> never, by itself, turns an anonymous request into
/// an authorized one. Publication state and media visibility are <em>eligibility</em> metadata —
/// they describe whether content <em>may</em> appear on a public projection surface (see
/// <see cref="PublicProjectionPolicy"/>), not whether an anonymous caller may fetch the private
/// resource itself. A public-approved product, contact, media asset, or event is therefore still
/// denied to an unauthenticated caller through every private read path.
/// </para>
/// <para>
/// Keeping the decision here as a pure function makes it the one place the rule lives and the
/// thing task 9.5 / Property 12 asserts across the whole input space. The API already denies
/// anonymous callers structurally: catalog, media, and administration read endpoints run behind
/// the Staff/Owner role policies or the fallback authenticated-user policy, and the only
/// endpoints that opt into <c>AllowAnonymous</c> are the first-run owner bootstrap and the CSRF /
/// login endpoints, none of which expose a private business resource. This policy expresses that
/// same guarantee as an explicit, testable business rule so the invariant cannot be weakened
/// silently by mistaking publication metadata for an access grant.
/// </para>
/// <para>
/// The policy is a pure static function over ordinary inputs — no I/O, clock, configuration,
/// <c>HttpContext</c>, or ambient state — so it is directly unit-testable without starting the
/// application (coding-standards §1, §3).
/// </para>
/// </remarks>
public static class PrivateResourceAccessPolicy
{
    /// <summary>
    /// Decides whether a caller may read a private business resource whose publication state is
    /// <paramref name="publicationState"/>.
    /// </summary>
    /// <param name="isAuthenticated">
    /// <see langword="true"/> when the caller presented a valid authenticated identity resolved at
    /// the API boundary; <see langword="false"/> for an anonymous caller.
    /// </param>
    /// <param name="publicationState">
    /// The resource's publication state. It is eligibility metadata only and never, by itself,
    /// grants anonymous access — a <see cref="PublicationState.PublicApproved"/> resource is still
    /// denied to an unauthenticated caller.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only when <paramref name="isAuthenticated"/> is
    /// <see langword="true"/>; otherwise <see langword="false"/>, regardless of
    /// <paramref name="publicationState"/>.
    /// </returns>
    public static bool CanRead(bool isAuthenticated, PublicationState publicationState)
    {
        // Publication state is deliberately read but never consulted as an access grant: the only
        // thing that authorizes a private read is a valid authenticated identity. Referencing the
        // parameter documents that even a public-approved resource is denied when anonymous, and
        // keeps the signature honest for callers that pass the resource's real state.
        _ = publicationState;
        return isAuthenticated;
    }

    /// <summary>
    /// Decides whether a caller may read a private media resource whose visibility is
    /// <paramref name="visibility"/>.
    /// </summary>
    /// <param name="isAuthenticated">
    /// <see langword="true"/> when the caller presented a valid authenticated identity resolved at
    /// the API boundary; <see langword="false"/> for an anonymous caller.
    /// </param>
    /// <param name="visibility">
    /// The asset's visibility. It is eligibility metadata only and never, by itself, grants
    /// anonymous access — a <see cref="MediaVisibility.PublicApproved"/> asset is still denied to
    /// an unauthenticated caller (requirement 6.11).
    /// </param>
    /// <returns>
    /// <see langword="true"/> only when <paramref name="isAuthenticated"/> is
    /// <see langword="true"/>; otherwise <see langword="false"/>, regardless of
    /// <paramref name="visibility"/>.
    /// </returns>
    public static bool CanRead(bool isAuthenticated, MediaVisibility visibility)
    {
        // Visibility is read but never used as a grant, mirroring the publication-state overload:
        // a public-approved asset is eligibility metadata, not an anonymous access token.
        _ = visibility;
        return isAuthenticated;
    }
}
