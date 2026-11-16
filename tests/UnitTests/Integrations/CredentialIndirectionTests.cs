using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Common;
using PenguinPlank.Domain.Integrations;

namespace UnitTests.Integrations;

/// <summary>
/// Credential reference validation and the credential-use policy (requirement R11 / 3.2, 3.3).
/// Pure, no I/O.
/// </summary>
public class CredentialIndirectionTests
{
    [Theory]
    [InlineData("keyvault://penguinplank/shopify-main")]
    [InlineData("secret/etsy-1")]
    public void Create_WellFormedLocator_ReturnsReference(string locator)
    {
        Result<CredentialReference> result = CredentialReference.Create(locator);

        Assert.True(result.IsSuccess);
        Assert.Equal(locator, result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("line\nbreak")]
    public void Create_EmptyOrMalformed_ReturnsValidationFailure(string? locator)
    {
        Result<CredentialReference> result = CredentialReference.Create(locator);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
    }

    [Fact]
    public void Create_OverMaximumLength_ReturnsValidationFailure()
    {
        string tooLong = new('a', CredentialReference.MaximumLength + 1);

        Result<CredentialReference> result = CredentialReference.Create(tooLong);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_AtMaximumLength_Succeeds()
    {
        string exact = new('a', CredentialReference.MaximumLength);

        Assert.True(CredentialReference.Create(exact).IsSuccess);
    }

    [Theory]
    [InlineData(null, CredentialState.Active, CredentialUseDecision.NoCredential)]
    [InlineData("  ", CredentialState.Active, CredentialUseDecision.NoCredential)]
    [InlineData("ref", CredentialState.Active, CredentialUseDecision.Usable)]
    [InlineData("ref", CredentialState.Revoked, CredentialUseDecision.Revoked)]
    [InlineData("ref", CredentialState.Expired, CredentialUseDecision.Expired)]
    [InlineData(null, CredentialState.Revoked, CredentialUseDecision.NoCredential)]
    public void Decide_ReferenceAndState_ReturnsExpectedDecision(
        string? reference,
        CredentialState state,
        CredentialUseDecision expected)
    {
        Assert.Equal(expected, CredentialUsePolicy.Decide(reference, state));
    }

    [Fact]
    public void NewConnection_Defaults_DisabledWithActiveCredentialState()
    {
        var connection = new IntegrationConnection();

        Assert.False(connection.Enabled);
        Assert.Equal(CredentialState.Active, connection.CredentialState);
        Assert.Null(connection.CredentialReference);
    }
}
