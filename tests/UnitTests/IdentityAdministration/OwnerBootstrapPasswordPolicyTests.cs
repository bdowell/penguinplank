using PenguinPlank.Application.IdentityAdministration;

namespace UnitTests.IdentityAdministration;

/// <summary>
/// Behavior of the pure <see cref="OwnerBootstrapPasswordPolicy"/>: it refuses a missing, blank,
/// or placeholder initial Owner password so there is no baked-in default (requirement A2 §5.8).
/// Pure, no I/O or database (coding-standards §1, §7).
/// </summary>
public class OwnerBootstrapPasswordPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void IsRefusedDefault_MissingOrBlank_IsRefused(string? initialPassword)
    {
        Assert.True(OwnerBootstrapPasswordPolicy.IsRefusedDefault(initialPassword));
    }

    [Theory]
    [InlineData("ChangeMe")]
    [InlineData("changeme")]
    [InlineData("CHANGEME")]
    [InlineData("change-me")]
    [InlineData("change_me")]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("default")]
    [InlineData("placeholder")]
    [InlineData("secret")]
    [InlineData("changeit")]
    [InlineData("todo")]
    [InlineData("  ChangeMe  ")]
    public void IsRefusedDefault_KnownPlaceholder_IsRefused(string initialPassword)
    {
        Assert.True(OwnerBootstrapPasswordPolicy.IsRefusedDefault(initialPassword));
    }

    [Theory]
    [InlineData("Zr7!qvW2m#Lx")]
    [InlineData("a-genuinely-chosen-operator-secret-9!")]
    [InlineData("NotAPlaceholderAtAll1$")]
    public void IsRefusedDefault_OperatorSuppliedSecret_IsAccepted(string initialPassword)
    {
        Assert.False(OwnerBootstrapPasswordPolicy.IsRefusedDefault(initialPassword));
    }
}
