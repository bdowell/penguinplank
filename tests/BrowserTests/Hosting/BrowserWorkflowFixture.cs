using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Playwright;

namespace BrowserTests.Hosting;

/// <summary>
/// Owns everything the browser smoke tests share for one run: the real API on an isolated SQL Server
/// database, the published Blazor client served same-origin, and one headless Chromium. Each test
/// opens its own isolated browser context, so sessions (cookies) never leak between tests, and
/// creates its own uniquely named records, so tests do not depend on each other's data.
/// </summary>
/// <remarks>
/// If the Playwright browser binaries are not installed, <see cref="InitializeAsync"/> fails with
/// Playwright's own "executable doesn't exist" message and every test in the collection fails; it
/// never skips or passes silently. Install them once with
/// <c>pwsh bin/&lt;config&gt;/&lt;tfm&gt;/playwright.ps1 install chromium</c>.
/// </remarks>
public sealed class BrowserWorkflowFixture : IAsyncLifetime, IAsyncDisposable
{
    private const float DefaultTimeoutMilliseconds = 60_000;

    private readonly string _publishDirectory =
        Path.Combine(Path.GetTempPath(), $"pp-web-browser-{Guid.NewGuid():N}");

    private BrowserApiFactory? _api;
    private HttpClient? _forwardingClient;
    private SameOriginHost? _host;
    private SeedApiClient? _seed;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>Gets the origin the browser opens, for example <c>http://localhost:5123</c>.</summary>
    public string BaseUrl => _host?.BaseUrl ?? throw NotInitialized();

    /// <summary>Gets the running API host, for arranging and inspecting server state.</summary>
    public BrowserApiFactory Api => _api ?? throw NotInitialized();

    /// <summary>Gets an API client signed in as the Owner, for arranging and inspecting server state.</summary>
    public SeedApiClient Seed => _seed ?? throw NotInitialized();

    /// <summary>Starts the database, API, static host, and browser.</summary>
    /// <returns>A task that completes when everything is ready.</returns>
    public async Task InitializeAsync()
    {
        _api = new BrowserApiFactory();
        await _api.InitializeAsync();

        string webRoot = await WebClientPublisher.PublishAsync(_publishDirectory, CancellationToken.None);

        // The forwarder must not manage cookies: the browser's own Cookie header is passed through.
        _forwardingClient = _api.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        _host = await SameOriginHost.StartAsync(webRoot, _forwardingClient);

        // Provision the first Owner once through the real one-time bootstrap and keep that Owner
        // signed in for arranging data, so every test can also sign the Owner in through the UI.
        _seed = await SeedApiClient.SignInOwnerAsync(_api);

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <summary>Stops the browser, hosts, and database and removes the published client.</summary>
    /// <returns>A task that completes when teardown finishes.</returns>
    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        _seed?.Dispose();

        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        _forwardingClient?.Dispose();

        if (_api is not null)
        {
            await _api.DisposeAsync();
        }

        if (Directory.Exists(_publishDirectory))
        {
            Directory.Delete(_publishDirectory, recursive: true);
        }
    }

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());

    /// <summary>Opens a new isolated browser context (its own cookies and storage).</summary>
    /// <param name="viewportWidth">The viewport width in CSS pixels.</param>
    /// <param name="viewportHeight">The viewport height in CSS pixels.</param>
    /// <returns>The new context, with a generous default timeout for first-load WebAssembly startup.</returns>
    public async Task<IBrowserContext> NewContextAsync(int viewportWidth = 1280, int viewportHeight = 800)
    {
        IBrowser browser = _browser ?? throw NotInitialized();

        IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = viewportWidth, Height = viewportHeight },
        });
        context.SetDefaultTimeout(DefaultTimeoutMilliseconds);
        return context;
    }

    /// <summary>Signs in through the real login page and waits for the signed-in shell.</summary>
    /// <param name="page">The page to drive.</param>
    /// <param name="email">The account email.</param>
    /// <param name="password">The account password.</param>
    /// <returns>A task that completes when the shell shows the signed-in actor.</returns>
    public async Task SignInAsync(IPage page, string email, string password)
    {
        ArgumentNullException.ThrowIfNull(page);

        await page.GotoAsync($"{BaseUrl}/login");
        await page.Locator("#login-email").FillAsync(email);
        await page.Locator("#login-password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign out" }))
            .ToBeVisibleAsync();
    }

    private static InvalidOperationException NotInitialized() =>
        new("The browser workflow fixture has not been initialized.");
}

/// <summary>
/// The xUnit collection sharing one <see cref="BrowserWorkflowFixture"/> (one database, one
/// published client, one browser) across the browser smoke-test classes. Classes in a collection run
/// one after another, which keeps the single browser and database free of cross-test contention.
/// </summary>
[CollectionDefinition(Name)]
public sealed class BrowserWorkflowTestGroup : ICollectionFixture<BrowserWorkflowFixture>
{
    /// <summary>The collection name shared by every browser smoke-test class.</summary>
    public const string Name = "BrowserWorkflow";
}
