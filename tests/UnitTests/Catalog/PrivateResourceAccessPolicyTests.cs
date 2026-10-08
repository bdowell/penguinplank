using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Media;

namespace UnitTests.Catalog;

/// <summary>
/// Behavior of the pure private-resource access decision (requirement A5 §7.4; design Property 12):
/// an unauthenticated caller is always denied a private product/contact/media/event resource, and
/// <see cref="PublicationState.PublicApproved"/> / <see cref="MediaVisibility.PublicApproved"/>
/// never, by itself, grants anonymous access. Pure, no I/O (coding-standards §1, §3). The
/// server-wide property coverage is task 9.5 / Property 12.
/// </summary>
public class PrivateResourceAccessPolicyTests
{
    [Theory]
    [InlineData(PublicationState.Draft)]
    [InlineData(PublicationState.PublicApproved)]
    public void CanRead_Unauthenticated_DeniedRegardlessOfPublicationState(PublicationState state)
    {
        // The key guarantee: a public-approved resource is still denied when the caller is
        // anonymous — publication metadata is eligibility, not an access grant.
        bool allowed = PrivateResourceAccessPolicy.CanRead(isAuthenticated: false, state);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(PublicationState.Draft)]
    [InlineData(PublicationState.PublicApproved)]
    public void CanRead_Authenticated_AllowedRegardlessOfPublicationState(PublicationState state)
    {
        bool allowed = PrivateResourceAccessPolicy.CanRead(isAuthenticated: true, state);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(MediaVisibility.Private)]
    [InlineData(MediaVisibility.PublicApproved)]
    public void CanRead_Unauthenticated_DeniedRegardlessOfMediaVisibility(MediaVisibility visibility)
    {
        // A public-approved media asset is never anonymously accessible (requirement 6.11).
        bool allowed = PrivateResourceAccessPolicy.CanRead(isAuthenticated: false, visibility);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(MediaVisibility.Private)]
    [InlineData(MediaVisibility.PublicApproved)]
    public void CanRead_Authenticated_AllowedRegardlessOfMediaVisibility(MediaVisibility visibility)
    {
        bool allowed = PrivateResourceAccessPolicy.CanRead(isAuthenticated: true, visibility);

        Assert.True(allowed);
    }

    [Fact]
    public void CanRead_PublicApprovedMetadata_DoesNotFlipAnAnonymousDenyToAnAllow()
    {
        // Explicitly contrast the two metadata values for an anonymous caller: both deny, so the
        // public-approved value does not act as an anonymous access token.
        bool draftAnon = PrivateResourceAccessPolicy.CanRead(false, PublicationState.Draft);
        bool approvedAnon = PrivateResourceAccessPolicy.CanRead(false, PublicationState.PublicApproved);

        Assert.False(draftAnon);
        Assert.False(approvedAnon);
        Assert.Equal(draftAnon, approvedAnon);
    }
}
