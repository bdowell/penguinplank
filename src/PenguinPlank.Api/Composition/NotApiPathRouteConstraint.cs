using System.Globalization;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Route constraint that rejects catch-all values under the <c>api</c> segment, so the Blazor
/// client fallback never answers an <c>/api</c> request with <c>index.html</c> (task 17.6).
/// An empty value (the site root) is allowed.
/// </summary>
public sealed class NotApiPathRouteConstraint : IRouteConstraint
{
    /// <summary>The name the constraint is registered under in route templates.</summary>
    public const string ConstraintName = "notapi";

    private const string ApiSegment = "api";

    /// <inheritdoc />
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        ArgumentNullException.ThrowIfNull(routeKey);
        ArgumentNullException.ThrowIfNull(values);

        values.TryGetValue(routeKey, out object? value);
        string? path = Convert.ToString(value, CultureInfo.InvariantCulture);
        return !IsApiPath(path);
    }

    /// <summary>
    /// Returns <see langword="true"/> when a catch-all route value (the request path without its
    /// leading slash) is <c>api</c> or lies under <c>api/</c>, compared case-insensitively.
    /// </summary>
    /// <param name="path">The catch-all route value; <see langword="null"/> or empty for the root.</param>
    public static bool IsApiPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string trimmed = path.TrimStart('/');
        if (!trimmed.StartsWith(ApiSegment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return trimmed.Length == ApiSegment.Length || trimmed[ApiSegment.Length] == '/';
    }
}
