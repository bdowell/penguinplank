using BrowserTests.Hosting;

namespace BrowserTests;

/// <summary>Fast, browser-free checks of the pure decisions the browser test harness makes.</summary>
public sealed class HostingHelperTests
{
    [Fact]
    public void ResolveConfiguration_BinConfigurationFrameworkLayout_ReturnsConfigurationFolder()
    {
        string baseDirectory = Path.Combine(Path.GetTempPath(), "repo", "bin", "Release", "net10.0") + Path.DirectorySeparatorChar;

        string configuration = WebClientPublisher.ResolveConfiguration(baseDirectory);

        Assert.Equal("Release", configuration);
    }

    [Fact]
    public void ResolveConfiguration_UnrecognizedLayout_FallsBackToDebug()
    {
        string baseDirectory = Path.Combine(Path.GetTempPath(), "somewhere", "else");

        string configuration = WebClientPublisher.ResolveConfiguration(baseDirectory);

        Assert.Equal("Debug", configuration);
    }

    [Theory]
    [InlineData("Host", true)]
    [InlineData("connection", true)]
    [InlineData("Transfer-Encoding", true)]
    [InlineData("Cookie", false)]
    [InlineData("X-CSRF-TOKEN", false)]
    public void IsExcludedFromRequest_HeaderName_DropsOnlyHostAndHopByHopHeaders(string headerName, bool expected)
    {
        Assert.Equal(expected, ProxyHeaderPolicy.IsExcludedFromRequest(headerName));
    }

    [Theory]
    [InlineData("Host", false)]
    [InlineData("Set-Cookie", false)]
    [InlineData("Keep-Alive", true)]
    public void IsExcludedFromResponse_HeaderName_DropsOnlyHopByHopHeaders(string headerName, bool expected)
    {
        Assert.Equal(expected, ProxyHeaderPolicy.IsExcludedFromResponse(headerName));
    }
}
