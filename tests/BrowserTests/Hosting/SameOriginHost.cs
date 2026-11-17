using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace BrowserTests.Hosting;

/// <summary>
/// A real Kestrel listener that serves the published Blazor client and forwards every
/// <c>/api</c> request to the in-process API, so the browser sees one origin exactly as in a
/// deployment. The client calls the API with relative same-origin URLs and relies on an HttpOnly
/// session cookie, so the page and the API must share an origin. The API can also host the client
/// (local development), but this harness serves the <em>published</em> client, the exact static files
/// a deployment ships, in front of the in-process test API.
/// </summary>
/// <remarks>
/// The forwarder passes the browser's own cookies and headers through unchanged, so the session
/// the browser holds is the session the API sees. It adds no authentication and makes no decision.
/// </remarks>
public sealed class SameOriginHost : IAsyncDisposable
{
    private const string ApiPathPrefix = "/api";
    private const string LoopbackAddress = "127.0.0.1";
    private const string BrowserHostName = "localhost";

    private readonly WebApplication _application;
    private readonly PhysicalFileProvider _files;

    private SameOriginHost(WebApplication application, PhysicalFileProvider files, string baseUrl)
    {
        _application = application;
        _files = files;
        BaseUrl = baseUrl;
    }

    /// <summary>Gets the origin the browser should open, without a trailing slash.</summary>
    public string BaseUrl { get; }

    /// <summary>Starts the host on a free loopback port.</summary>
    /// <param name="webRoot">The published client <c>wwwroot</c> directory to serve.</param>
    /// <param name="apiClient">An HTTP client bound to the in-process API; it must not handle cookies itself.</param>
    /// <returns>The started host.</returns>
    public static async Task<SameOriginHost> StartAsync(string webRoot, HttpClient apiClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webRoot);
        ArgumentNullException.ThrowIfNull(apiClient);

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://{LoopbackAddress}:0");
        builder.Logging.ClearProviders();

        WebApplication application = builder.Build();
        PhysicalFileProvider files = new(webRoot);

        application.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(ApiPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                await ForwardToApiAsync(context, apiClient);
                return;
            }

            await next(context);
        });

        application.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ServeUnknownFileTypes = true,
        });

        // Client-side routes (/login, /catalog/...) resolve to the single-page entry document.
        application.MapFallback(async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(files.GetFileInfo("index.html"));
        });

        await application.StartAsync();

        IServerAddressesFeature addresses = application.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The test host reported no listening address.");
        int port = new Uri(addresses.Addresses.First()).Port;

        // Browsers treat "localhost" as a secure context, which the Secure-flagged session cookie needs
        // over plain HTTP; the listener is bound to the IPv4 loopback address only.
        return new SameOriginHost(application, files, $"http://{BrowserHostName}:{port}");
    }

    /// <summary>Stops the listener and releases the served files.</summary>
    /// <returns>A task that completes when the host has stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
        _files.Dispose();
    }

    private static async Task ForwardToApiAsync(HttpContext context, HttpClient apiClient)
    {
        HttpRequest request = context.Request;
        using HttpRequestMessage forwarded = new(
            new HttpMethod(request.Method),
            $"{request.Path}{request.QueryString}");

        if (request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding"))
        {
            forwarded.Content = new StreamContent(request.Body);
        }

        foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in request.Headers)
        {
            if (ProxyHeaderPolicy.IsExcludedFromRequest(header.Key))
            {
                continue;
            }

            string[] values = header.Value.ToArray()!;
            if (!forwarded.Headers.TryAddWithoutValidation(header.Key, values))
            {
                forwarded.Content?.Headers.TryAddWithoutValidation(header.Key, values);
            }
        }

        using HttpResponseMessage response = await apiClient.SendAsync(
            forwarded,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);

        context.Response.StatusCode = (int)response.StatusCode;
        CopyHeaders(response.Headers, context.Response);
        CopyHeaders(response.Content.Headers, context.Response);

        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    private static void CopyHeaders(
        System.Net.Http.Headers.HttpHeaders source,
        HttpResponse destination)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in source)
        {
            if (!ProxyHeaderPolicy.IsExcludedFromResponse(header.Key))
            {
                destination.Headers.Append(header.Key, header.Value.ToArray());
            }
        }
    }
}
