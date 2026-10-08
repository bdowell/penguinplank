using CsCheck;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Media;

namespace UnitTests.Catalog;

/// <summary>
/// Property 12 (task 9.5, requirements A4 §6.11, A5 §7.4): <em>Unauthorized callers are denied
/// private data.</em> <see cref="PrivateResourceAccessPolicy"/> is a pure static decision over
/// ordinary inputs — no I/O, clock, configuration, <c>HttpContext</c>, or ambient state — so these
/// properties exercise it across the whole input space without starting the application
/// (coding-standards §1, §3). They assert, over <em>every</em> <see cref="PublicationState"/> and
/// <see cref="MediaVisibility"/> value, that:
/// <list type="number">
///   <item><b>Unauthenticated is always denied.</b> For every publication state and every media
///   visibility, an anonymous caller is denied — and crucially
///   <see cref="PublicationState.PublicApproved"/> / <see cref="MediaVisibility.PublicApproved"/>
///   never, by itself, grants anonymous access. Publication/visibility is eligibility metadata,
///   never an access grant.</item>
///   <item><b>Metadata does not flip the decision for an anonymous caller.</b> The decision for an
///   anonymous caller is identical regardless of the publication/visibility value, so the
///   public-approved value cannot act as an anonymous access token.</item>
///   <item><b>Authentication is the gate.</b> The policy's defined behavior is that access is
///   granted exactly when the caller is authenticated, independent of the metadata value.</item>
/// </list>
/// </summary>
/// <remarks>
/// Generators enumerate the full set of <see cref="PublicationState"/> and
/// <see cref="MediaVisibility"/> values (including the public-approved value) combined with both
/// authentication states, so the "unauthenticated ⇒ denied regardless of public-approved metadata"
/// invariant is sampled across the entire input space. A failing counterexample here would signal
/// a real authorization defect (publication metadata leaking into an access grant), not a test to
/// weaken. CsCheck (pinned in <c>UnitTests.csproj</c>) runs each property at <see cref="Iterations"/>
/// samples, well above the design's ≥100 floor.
///
/// <b>Validates: Requirements 6.11, 7.4</b>
/// </remarks>
public class UnauthorizedAccessDeniedPropertyTests
{
    /// <summary>
    /// Iterations per CsCheck property. The design mandates ≥100 iterations for every correctness
    /// property; this sits well above that floor for a wider sample of the input space.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>Generates every defined <see cref="PublicationState"/> value (Draft, PublicApproved).</summary>
    private static readonly Gen<PublicationState> s_genPublicationState =
        Gen.Int[0, 1].Select(index => (PublicationState)index);

    /// <summary>Generates every defined <see cref="MediaVisibility"/> value (Private, PublicApproved).</summary>
    private static readonly Gen<MediaVisibility> s_genMediaVisibility =
        Gen.Int[0, 1].Select(index => (MediaVisibility)index);

    [Fact]
    public void CanRead_Unauthenticated_DeniedForEveryPublicationStateIncludingPublicApproved()
    {
        s_genPublicationState.Sample(
            state =>
            {
                bool allowed = PrivateResourceAccessPolicy.CanRead(isAuthenticated: false, state);

                // The key guarantee: a public-approved resource is still denied to an anonymous
                // caller — publication metadata is eligibility, never an access grant.
                Assert.False(allowed);
            },
            iter: Iterations);
    }

    [Fact]
    public void CanRead_Unauthenticated_DeniedForEveryMediaVisibilityIncludingPublicApproved()
    {
        s_genMediaVisibility.Sample(
            visibility =>
            {
                bool allowed = PrivateResourceAccessPolicy.CanRead(isAuthenticated: false, visibility);

                // A public-approved media asset is never anonymously accessible (requirement 6.11).
                Assert.False(allowed);
            },
            iter: Iterations);
    }

    [Fact]
    public void CanRead_PublicationState_GrantsExactlyWhenAuthenticatedRegardlessOfMetadata()
    {
        Gen.Select(Gen.Bool, s_genPublicationState).Sample(
            input =>
            {
                (bool isAuthenticated, PublicationState state) = input;

                bool allowed = PrivateResourceAccessPolicy.CanRead(isAuthenticated, state);

                // Authentication is the gate: the outcome equals isAuthenticated for every
                // publication state, so the metadata value never changes the decision.
                Assert.Equal(isAuthenticated, allowed);
            },
            iter: Iterations);
    }

    [Fact]
    public void CanRead_MediaVisibility_GrantsExactlyWhenAuthenticatedRegardlessOfMetadata()
    {
        Gen.Select(Gen.Bool, s_genMediaVisibility).Sample(
            input =>
            {
                (bool isAuthenticated, MediaVisibility visibility) = input;

                bool allowed = PrivateResourceAccessPolicy.CanRead(isAuthenticated, visibility);

                // Authentication is the gate: the outcome equals isAuthenticated for every
                // visibility value, so the metadata value never changes the decision.
                Assert.Equal(isAuthenticated, allowed);
            },
            iter: Iterations);
    }

    [Fact]
    public void CanRead_Unauthenticated_PublicationMetadataNeverFlipsTheAnonymousDecision()
    {
        s_genPublicationState.Sample(
            state =>
            {
                // For an anonymous caller the decision is independent of the publication state: it
                // matches the Draft decision for every state, so PublicApproved is not an access
                // token.
                bool anonForState = PrivateResourceAccessPolicy.CanRead(false, state);
                bool anonForDraft = PrivateResourceAccessPolicy.CanRead(false, PublicationState.Draft);

                Assert.Equal(anonForDraft, anonForState);
                Assert.False(anonForState);
            },
            iter: Iterations);
    }

    [Fact]
    public void CanRead_Unauthenticated_MediaVisibilityNeverFlipsTheAnonymousDecision()
    {
        s_genMediaVisibility.Sample(
            visibility =>
            {
                bool anonForVisibility = PrivateResourceAccessPolicy.CanRead(false, visibility);
                bool anonForPrivate = PrivateResourceAccessPolicy.CanRead(false, MediaVisibility.Private);

                Assert.Equal(anonForPrivate, anonForVisibility);
                Assert.False(anonForVisibility);
            },
            iter: Iterations);
    }
}
