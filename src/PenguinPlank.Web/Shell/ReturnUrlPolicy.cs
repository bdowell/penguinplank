namespace PenguinPlank.Web.Shell;

/// <summary>Validates post-login return targets so login cannot redirect off-site.</summary>
public static class ReturnUrlPolicy
{
    /// <summary>The route of the login page, without a leading slash.</summary>
    public const string LoginRoute = "login";

    /// <summary>Resolves the relative URI to navigate to after sign-in.</summary>
    /// <param name="candidate">The requested return target, such as a query-string value.</param>
    /// <returns>
    /// The candidate when it is a same-origin relative path other than the login page; otherwise
    /// the application root (an empty relative URI).
    /// </returns>
    public static string Resolve(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        string path = candidate.TrimStart('/');
        bool isRootedPath = candidate.StartsWith('/') && !candidate.StartsWith("//", StringComparison.Ordinal);
        bool hasUnsafeCharacter = candidate.Contains('\\') || candidate.Any(char.IsControl);
        bool isLoginPage = path.Equals(LoginRoute, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(LoginRoute + "?", StringComparison.OrdinalIgnoreCase);

        return isRootedPath && !hasUnsafeCharacter && !isLoginPage ? path : string.Empty;
    }

    /// <summary>Builds the login URL that returns to a protected location afterwards.</summary>
    /// <param name="currentRelativeUri">The location the user attempted to open, relative to the base URI.</param>
    /// <returns>A relative login URL.</returns>
    public static string BuildLoginUrl(string? currentRelativeUri)
    {
        string target = Resolve("/" + (currentRelativeUri ?? string.Empty).TrimStart('/'));
        return target.Length == 0
            ? LoginRoute
            : $"{LoginRoute}?returnUrl={Uri.EscapeDataString("/" + target)}";
    }
}
