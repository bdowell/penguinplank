using System.Security.Claims;
using PenguinPlank.Api.Endpoints;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.IdentityAdministration.Authorization;

namespace UnitTests.Catalog;

/// <summary>
/// Behavior of <see cref="ActorContextResolver"/> that builds the explicit
/// <see cref="ActorContext"/> from the authenticated principal's claims at the API boundary, so
/// use cases never touch <c>HttpContext</c> (task 9.1, requirement A2 §5.12).
/// </summary>
public class ActorContextResolverTests
{
    [Fact]
    public void Resolve_OwnerPrincipal_MapsUserIdAndOwnerRole()
    {
        Guid userId = Guid.NewGuid();
        ClaimsPrincipal principal = BuildPrincipal(userId, RoleNames.Owner);

        ActorContext actor = ActorContextResolver.Resolve(principal);

        Assert.Equal(userId, actor.UserId);
        Assert.Equal(Role.Owner, actor.Role);
        Assert.False(actor.IsSystemWorker);
    }

    [Fact]
    public void Resolve_StaffPrincipal_MapsStaffRole()
    {
        ClaimsPrincipal principal = BuildPrincipal(Guid.NewGuid(), RoleNames.Staff);

        ActorContext actor = ActorContextResolver.Resolve(principal);

        Assert.Equal(Role.Staff, actor.Role);
    }

    [Fact]
    public void Resolve_PrincipalWithBothRoles_PrefersOwnerAuthority()
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, RoleNames.Staff));
        identity.AddClaim(new Claim(ClaimTypes.Role, RoleNames.Owner));

        ActorContext actor = ActorContextResolver.Resolve(new ClaimsPrincipal(identity));

        Assert.Equal(Role.Owner, actor.Role);
    }

    [Fact]
    public void Resolve_MissingUserId_Throws()
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.Role, RoleNames.Owner));

        Assert.Throws<InvalidOperationException>(() => ActorContextResolver.Resolve(new ClaimsPrincipal(identity)));
    }

    [Fact]
    public void Resolve_MissingRole_Throws()
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        Assert.Throws<InvalidOperationException>(() => ActorContextResolver.Resolve(new ClaimsPrincipal(identity)));
    }

    private static ClaimsPrincipal BuildPrincipal(Guid userId, string roleName)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
        return new ClaimsPrincipal(identity);
    }
}
