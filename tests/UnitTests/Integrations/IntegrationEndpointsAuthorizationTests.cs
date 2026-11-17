using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using PenguinPlank.Api.Composition;
using PenguinPlank.Api.Endpoints;
using PenguinPlank.Application.IdentityAdministration.Authorization;

namespace UnitTests.Integrations;

/// <summary>
/// Verifies that every integration management route is mapped under the Owner-only policy so Staff
/// cannot reach credential state or integration controls (task 13.3; requirement A7 / 9.5).
/// </summary>
/// <remarks>
/// The routes are mapped onto a host built with the real composition root and an empty
/// configuration (no database), and the route metadata is inspected. No request is sent; the
/// end-to-end Staff 403 is covered by the acceptance scenario #21 integration test.
/// </remarks>
public class IntegrationEndpointsAuthorizationTests
{
    private static readonly string[] s_expectedRoutes =
    [
        "GET /api/v1/integrations/connections/",
        "GET /api/v1/integrations/connections/{id:guid}",
        "POST /api/v1/integrations/connections/",
        "POST /api/v1/integrations/connections/{id:guid}/pause",
        "POST /api/v1/integrations/connections/{id:guid}/resume",
        "GET /api/v1/integrations/mappings/",
        "POST /api/v1/integrations/mappings/",
        "GET /api/v1/integrations/sync-runs",
        "GET /api/v1/integrations/exceptions/",
        "POST /api/v1/integrations/exceptions/{id:guid}/retry",
        "POST /api/v1/integrations/exceptions/{id:guid}/reconcile",
    ];

    [Fact]
    public async Task MapIntegrationEndpoints_EveryRoute_RequiresOwnerOnlyPolicyAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Services.AddPenguinPlankServices(new ConfigurationBuilder().Build());
        await using WebApplication app = builder.Build();

        app.MapIntegrationEndpoints();

        RouteEndpoint[] endpoints =
        [
            .. ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>(),
        ];

        string[] routes = [.. endpoints.Select(Describe)];
        Assert.Equal(s_expectedRoutes.Order(), routes.Order());

        Assert.All(endpoints, endpoint =>
        {
            IReadOnlyList<IAuthorizeData> authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            Assert.Contains(authorize, data => data.Policy == AuthorizationPolicyNames.OwnerOnly);
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        });
    }

    private static string Describe(RouteEndpoint endpoint)
    {
        string method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Single() ?? "ANY";
        return $"{method} {endpoint.RoutePattern.RawText}";
    }
}
