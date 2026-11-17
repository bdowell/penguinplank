using System.Text.Json;
using PenguinPlank.Api.Endpoints;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Contracts.Integrations;
using PenguinPlank.Domain.Integrations;

namespace UnitTests.Integrations;

/// <summary>
/// Behavior of <see cref="IntegrationContractMapper"/>, the boundary translation behind the
/// Owner-only integration management endpoints (task 13.3; requirements 3.2, 3.12).
/// </summary>
public class IntegrationContractMapperTests
{
    private static readonly string[] s_allowedConnectionFields =
    [
        "id",
        "platform",
        "account",
        "scopes",
        "apiVersion",
        "enabledCapabilities",
        "syncDirection",
        "health",
        "enabled",
        "hasCredential",
        "credentialState",
        "credentialStateChangedAtUtc",
    ];

    [Theory]
    [InlineData("Inbound", SyncDirection.Inbound)]
    [InlineData("bidirectional", SyncDirection.Bidirectional)]
    [InlineData("None", SyncDirection.None)]
    public void ToRegistration_KnownDirection_MapsAllFields(string direction, SyncDirection expected)
    {
        RegisterConnectionContract body = NewRegistration(direction);

        ConnectionRegistration registration = IntegrationContractMapper.ToRegistration(body);

        Assert.Equal(expected, registration.SyncDirection);
        Assert.Equal("Shopify", registration.Platform);
        Assert.Equal("penguin-shop", registration.Account);
        Assert.Equal("read_orders", registration.Scopes);
        Assert.Equal("2024-10", registration.ApiVersion);
        Assert.Equal("orders", registration.EnabledCapabilities);
    }

    [Theory]
    [InlineData("Sideways")]
    [InlineData("")]
    [InlineData("42")]
    public void ToRegistration_UnknownDirection_ThrowsFormatException(string direction)
    {
        RegisterConnectionContract body = NewRegistration(direction);

        Assert.Throws<CatalogContractFormatException>(() => IntegrationContractMapper.ToRegistration(body));
    }

    [Fact]
    public void ToResponse_Connection_SerializesOnlyCredentialFreeFields()
    {
        var summary = new IntegrationConnectionSummary(
            Guid.NewGuid(),
            "Shopify",
            "penguin-shop",
            "read_orders",
            "2024-10",
            "orders",
            SyncDirection.Inbound,
            ConnectionHealth.Unknown,
            Enabled: false,
            HasCredential: true,
            CredentialState.Revoked,
            new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero));

        ConnectionResponse response = IntegrationContractMapper.ToResponse(summary);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(response, JsonSerializerOptions.Web));

        string[] fields = [.. json.RootElement.EnumerateObject().Select(property => property.Name)];
        Assert.Equal(s_allowedConnectionFields.Order(), fields.Order());
        Assert.Equal("Revoked", response.CredentialState);
        Assert.Equal("Inbound", response.SyncDirection);
        Assert.False(response.Enabled);
    }

    [Fact]
    public void ToResponse_SyncException_CarriesStatusAndAvailableActions()
    {
        var record = new SyncExceptionRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "orders/1001",
            "Permission denied [redacted]",
            SyncExceptionStatus.Open,
            new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var view = new SyncExceptionView(record, SyncExceptionActions.Available(record.Status));

        SyncExceptionResponse response = IntegrationContractMapper.ToResponse(view);

        Assert.Equal(record.Id, response.Id);
        Assert.Equal("Open", response.Status);
        Assert.Equal("Permission denied [redacted]", response.RedactedError);
        Assert.Equal(["Retry", "Reconcile"], response.AvailableActions);
    }

    [Fact]
    public void ToRequest_Mapping_PreservesIdentifiers()
    {
        var body = new CreateMappingContract
        {
            ConnectionId = Guid.NewGuid(),
            Platform = "Shopify",
            Account = "penguin-shop",
            ExternalId = "gid://shopify/ProductVariant/1",
            OwnerType = "ProductVariant",
            InternalId = Guid.NewGuid(),
        };

        EntityMappingRequest request = IntegrationContractMapper.ToRequest(body);

        Assert.Equal(
            new EntityMappingRequest(body.ConnectionId, body.Platform, body.Account, body.ExternalId, body.OwnerType, body.InternalId),
            request);
    }

    private static RegisterConnectionContract NewRegistration(string direction) => new()
    {
        Platform = "Shopify",
        Account = "penguin-shop",
        Scopes = "read_orders",
        ApiVersion = "2024-10",
        EnabledCapabilities = "orders",
        SyncDirection = direction,
    };
}
