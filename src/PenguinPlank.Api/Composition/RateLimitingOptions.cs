namespace PenguinPlank.Api.Composition;

/// <summary>
/// Typed, validated rate-limit policy values (requirement A6 §8.5 abuse protection). Bound from the
/// <c>RateLimiting</c> configuration section; the defaults are a conservative private-business
/// baseline.
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>The policy name applied to login and the owner bootstrap (anonymous, brute-force target).</summary>
    public const string LoginPolicyName = "login";

    /// <summary>The policy name applied to costly operations (media upload, audit queries).</summary>
    public const string CostlyPolicyName = "costly";

    /// <summary>Gets or sets the permitted login attempts per client address per window.</summary>
    public int LoginPermitLimit { get; set; } = 10;

    /// <summary>Gets or sets the login window length in seconds.</summary>
    public int LoginWindowSeconds { get; set; } = 60;

    /// <summary>Gets or sets the permitted costly requests per actor per window.</summary>
    public int CostlyPermitLimit { get; set; } = 30;

    /// <summary>Gets or sets the costly-operation window length in seconds.</summary>
    public int CostlyWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Checks that every limit and window is positive.
    /// </summary>
    /// <returns><see langword="true"/> when the options are usable.</returns>
    public bool IsValid()
    {
        return LoginPermitLimit > 0
            && LoginWindowSeconds > 0
            && CostlyPermitLimit > 0
            && CostlyWindowSeconds > 0;
    }
}
