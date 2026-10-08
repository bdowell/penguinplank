using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using PenguinPlank.Api.Endpoints;

namespace UnitTests.Catalog;

/// <summary>
/// Behavior of <see cref="MutationHeaders"/> that reads the <c>Idempotency-Key</c> and
/// <c>If-Match</c> mutation headers and formats an <c>ETag</c> response value (task 9.1,
/// requirements A4 §6.2, §6.5).
/// </summary>
/// <remarks>
/// These assert the small header parsing/formatting rules the thin endpoints depend on — unquoting
/// a strong or weak <c>If-Match</c> ETag so it matches the token a prior read returned, and
/// round-tripping the <c>ETag</c> response value — against an in-memory
/// <see cref="DefaultHttpContext"/> with no application startup (coding-standards §1, §7).
/// </remarks>
public class MutationHeadersTests
{
    [Fact]
    public void TryGetIdempotencyKey_PresentNonEmpty_ReturnsTrueAndValue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[MutationHeaders.IdempotencyKeyHeader] = "key-abc";

        bool found = MutationHeaders.TryGetIdempotencyKey(httpContext, out string? key);

        Assert.True(found);
        Assert.Equal("key-abc", key);
    }

    [Fact]
    public void TryGetIdempotencyKey_Missing_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();

        bool found = MutationHeaders.TryGetIdempotencyKey(httpContext, out string? key);

        Assert.False(found);
        Assert.Null(key);
    }

    [Theory]
    [InlineData("\"abc123\"", "abc123")]
    [InlineData("W/\"abc123\"", "abc123")]
    [InlineData("abc123", "abc123")]
    public void TryGetIfMatch_UnquotesStrongAndWeakETags(string headerValue, string expected)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[HeaderNames.IfMatch] = headerValue;

        bool found = MutationHeaders.TryGetIfMatch(httpContext, out string? expectedVersion);

        Assert.True(found);
        Assert.Equal(expected, expectedVersion);
    }

    [Fact]
    public void TryGetIfMatch_Missing_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();

        bool found = MutationHeaders.TryGetIfMatch(httpContext, out string? expectedVersion);

        Assert.False(found);
        Assert.Null(expectedVersion);
    }

    [Fact]
    public void ToETagHeader_WrapsTokenInQuotes()
    {
        Assert.Equal("\"token-123\"", MutationHeaders.ToETagHeader("token-123"));
    }
}
