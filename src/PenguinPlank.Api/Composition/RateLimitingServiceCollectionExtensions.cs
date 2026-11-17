using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Composition-root registration of the fixed-window rate limiter (requirement A6 §8.5).
/// </summary>
/// <remarks>
/// Limits are partitioned per client: the login policy by remote address (the caller has no session
/// yet), the costly policy by authenticated user id, falling back to remote address. A rejected
/// request receives <c>429</c>, rendered as ProblemDetails by the registered ProblemDetails service.
/// Behind a reverse proxy, configure forwarded headers so the remote address is the real client.
/// </remarks>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>The partition key used when a request has neither a user id nor a remote address.</summary>
    public const string UnknownClientKey = "ip:unknown";

    /// <summary>
    /// Registers the validated options and the login and costly rate-limit policies.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">Configuration holding the optional <c>RateLimiting</c> section.</param>
    /// <returns>The same <paramref name="services"/> instance.</returns>
    public static IServiceCollection AddPenguinPlankRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        System.ArgumentNullException.ThrowIfNull(services);
        System.ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .Validate(options => options.IsValid(), "RateLimiting options require positive limits and windows.")
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        // Policies read the validated options when the limiter options are first resolved, so they
        // stay configuration-driven.
        services
            .AddOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((limiter, settings) =>
            {
                RateLimitingOptions values = settings.Value;

                limiter.AddPolicy(RateLimitingOptions.LoginPolicyName, context =>
                    CreatePartition(
                        ResolveClientKey(context, useUser: false),
                        values.LoginPermitLimit,
                        values.LoginWindowSeconds));

                limiter.AddPolicy(RateLimitingOptions.CostlyPolicyName, context =>
                    CreatePartition(
                        ResolveClientKey(context, useUser: true),
                        values.CostlyPermitLimit,
                        values.CostlyWindowSeconds));
            });

        return services;
    }

    /// <summary>
    /// Builds the fixed-window partition for a client key.
    /// </summary>
    /// <param name="partitionKey">The client key (user id or remote address).</param>
    /// <param name="permitLimit">Permits per window.</param>
    /// <param name="windowSeconds">Window length in seconds.</param>
    /// <returns>The rate-limit partition.</returns>
    public static RateLimitPartition<string> CreatePartition(string partitionKey, int permitLimit, int windowSeconds)
    {
        // Kestrel always supplies a remote address, so an unknown client only occurs with an
        // in-process test server (no socket). Throttling that case would put every in-process
        // test caller in one shared bucket; it is left unlimited instead.
        if (partitionKey == UnknownClientKey)
        {
            return RateLimitPartition.GetNoLimiter(partitionKey);
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }

    /// <summary>
    /// Chooses the partition key for a request: the authenticated user id when requested and
    /// present, otherwise the remote address, otherwise <see cref="UnknownClientKey"/>.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="useUser">Whether to prefer the authenticated user id.</param>
    /// <returns>A stable partition key.</returns>
    public static string ResolveClientKey(HttpContext context, bool useUser)
    {
        if (useUser)
        {
            string? userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                return "user:" + userId;
            }
        }

        string? address = context.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrEmpty(address) ? UnknownClientKey : "ip:" + address;
    }
}
