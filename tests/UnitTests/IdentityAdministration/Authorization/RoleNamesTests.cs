using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.IdentityAdministration.Authorization;

namespace UnitTests.IdentityAdministration.Authorization;

/// <summary>
/// Stability of the role-name contract shared by Identity, authorization policies, and the
/// strongly typed <see cref="Role"/> enum (requirement A2 §5.10). Pure, no I/O.
/// </summary>
public class RoleNamesTests
{
    [Fact]
    public void Names_AreTheStableContractStrings()
    {
        Assert.Equal("Owner", RoleNames.Owner);
        Assert.Equal("Staff", RoleNames.Staff);
    }

    [Theory]
    [InlineData(Role.Owner, "Owner")]
    [InlineData(Role.Staff, "Staff")]
    public void FromRole_MapsEnumToCanonicalName(Role role, string expected)
    {
        Assert.Equal(expected, RoleNames.FromRole(role));
    }

    [Fact]
    public void FromRole_UndefinedRole_Throws()
    {
        Assert.Throws<System.ComponentModel.InvalidEnumArgumentException>(
            () => RoleNames.FromRole((Role)999));
    }
}
