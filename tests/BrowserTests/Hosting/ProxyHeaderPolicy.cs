namespace BrowserTests.Hosting;

/// <summary>
/// Decides which HTTP headers the same-origin test host must not copy when it forwards a request
/// to the in-process API (or a response back to the browser). Hop-by-hop headers describe a single
/// connection, so forwarding them would corrupt the next hop.
/// </summary>
public static class ProxyHeaderPolicy
{
    private static readonly HashSet<string> s_hopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade",
    };

    /// <summary>Determines whether a header must be dropped when forwarding a request.</summary>
    /// <param name="headerName">The header name.</param>
    /// <returns><see langword="true"/> when the header must not be forwarded.</returns>
    public static bool IsExcludedFromRequest(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);

        // The Host header is rebuilt from the API client's base address.
        return string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase)
            || s_hopByHopHeaders.Contains(headerName);
    }

    /// <summary>Determines whether a header must be dropped when forwarding a response.</summary>
    /// <param name="headerName">The header name.</param>
    /// <returns><see langword="true"/> when the header must not be forwarded.</returns>
    public static bool IsExcludedFromResponse(string headerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        return s_hopByHopHeaders.Contains(headerName);
    }
}
