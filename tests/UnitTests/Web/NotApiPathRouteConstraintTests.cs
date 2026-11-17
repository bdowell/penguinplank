using Microsoft.AspNetCore.Routing;
using PenguinPlank.Api.Composition;

namespace UnitTests.Web;

/// <summary>
/// Task 17.6: the client fallback route must skip /api paths (so they stay 404/401 instead of
/// returning index.html) while still serving the site root and client routes.
/// </summary>
public sealed class NotApiPathRouteConstraintTests
{
    [Theory]
    [InlineData("api")]
    [InlineData("api/")]
    [InlineData("api/v1/does-not-exist")]
    [InlineData("API/v1/products")]
    [InlineData("/api/unknown")]
    public void IsApiPath_ApiSegment_ReturnsTrue(string path)
    {
        Assert.True(NotApiPathRouteConstraint.IsApiPath(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("catalog")]
    [InlineData("apiary")]
    [InlineData("apis/list")]
    [InlineData("products/api")]
    public void IsApiPath_NonApiPath_ReturnsFalse(string? path)
    {
        Assert.False(NotApiPathRouteConstraint.IsApiPath(path));
    }

    [Fact]
    public void Match_ApiValue_RejectsFallback()
    {
        NotApiPathRouteConstraint constraint = new();
        RouteValueDictionary values = new() { ["path"] = "api/v1/missing" };

        bool matched = constraint.Match(null, null, "path", values, RouteDirection.IncomingRequest);

        Assert.False(matched);
    }

    [Fact]
    public void Match_MissingValue_AllowsRootFallback()
    {
        NotApiPathRouteConstraint constraint = new();

        bool matched = constraint.Match(null, null, "path", new RouteValueDictionary(), RouteDirection.IncomingRequest);

        Assert.True(matched);
    }
}
