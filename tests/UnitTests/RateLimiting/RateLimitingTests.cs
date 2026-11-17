using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using PenguinPlank.Api.Composition;

namespace UnitTests.RateLimiting;

/// <summary>
/// Behavior of the rate-limit options and partitioning (requirement A6 §8.5), exercised without
/// starting the application.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public void IsValid_Defaults_ReturnsTrue()
    {
        Assert.True(new RateLimitingOptions().IsValid());
    }

    [Theory]
    [InlineData(0, 60, 30, 60)]
    [InlineData(10, 0, 30, 60)]
    [InlineData(10, 60, -1, 60)]
    [InlineData(10, 60, 30, 0)]
    public void IsValid_NonPositiveValue_ReturnsFalse(int loginLimit, int loginWindow, int costlyLimit, int costlyWindow)
    {
        var options = new RateLimitingOptions
        {
            LoginPermitLimit = loginLimit,
            LoginWindowSeconds = loginWindow,
            CostlyPermitLimit = costlyLimit,
            CostlyWindowSeconds = costlyWindow,
        };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void CreatePartition_ExceedingPermitLimit_RejectsExtraRequest()
    {
        RateLimitPartition<string> partition = RateLimitingServiceCollectionExtensions.CreatePartition("ip:203.0.113.5", 2, 60);
        using RateLimiter limiter = partition.Factory("ip:203.0.113.5");

        using RateLimitLease first = limiter.AttemptAcquire();
        using RateLimitLease second = limiter.AttemptAcquire();
        using RateLimitLease third = limiter.AttemptAcquire();

        Assert.True(first.IsAcquired);
        Assert.True(second.IsAcquired);
        Assert.False(third.IsAcquired);
    }

    [Fact]
    public void CreatePartition_UnknownClient_IsNotThrottled()
    {
        RateLimitPartition<string> partition = RateLimitingServiceCollectionExtensions.CreatePartition(
            RateLimitingServiceCollectionExtensions.UnknownClientKey, 1, 60);
        using RateLimiter limiter = partition.Factory(partition.PartitionKey);

        using RateLimitLease first = limiter.AttemptAcquire();
        using RateLimitLease second = limiter.AttemptAcquire();

        Assert.True(first.IsAcquired);
        Assert.True(second.IsAcquired);
    }

    [Fact]
    public void ResolveClientKey_AuthenticatedUserForCostly_UsesUserId()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u-1")], "test"));
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");

        Assert.Equal("user:u-1", RateLimitingServiceCollectionExtensions.ResolveClientKey(context, useUser: true));
    }

    [Fact]
    public void ResolveClientKey_LoginPolicy_IgnoresUserAndUsesAddress()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u-1")], "test"));
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");

        Assert.Equal("ip:203.0.113.5", RateLimitingServiceCollectionExtensions.ResolveClientKey(context, useUser: false));
    }

    [Fact]
    public void ResolveClientKey_NoAddressOrUser_UsesSharedUnknownBucket()
    {
        var context = new DefaultHttpContext();

        Assert.Equal("ip:unknown", RateLimitingServiceCollectionExtensions.ResolveClientKey(context, useUser: true));
    }
}
