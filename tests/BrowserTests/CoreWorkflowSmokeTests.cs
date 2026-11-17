using System.Text.RegularExpressions;
using BrowserTests.Hosting;
using Microsoft.Playwright;
using PenguinPlank.Contracts.Catalog;

namespace BrowserTests;

/// <summary>
/// Core-workflow smoke tests driving the real Blazor client in Chromium against the real API and
/// SQL Server (requirements A5 7.5: usable catalog workflows, including at narrow widths).
/// </summary>
/// <remarks>
/// Phase A's client has no create screens, so the scenarios arrange products and variants through
/// the API and then perform every edit through the browser UI, asserting the persisted result by
/// reading the API afterwards.
/// </remarks>
[Collection(BrowserWorkflowTestGroup.Name)]
public sealed class CoreWorkflowSmokeTests
{
    private const int NarrowViewportWidth = 360;
    private const int NarrowViewportHeight = 640;
    private const string StaffPassword = "Staff!Browser#2025";

    private readonly BrowserWorkflowFixture _fixture;

    public CoreWorkflowSmokeTests(BrowserWorkflowFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_ValidOwnerCredentials_ShowsSignedInShellAsync()
    {
        // Arrange
        await using IBrowserContext context = await _fixture.NewContextAsync();
        IPage page = await context.NewPageAsync();

        // Act
        await _fixture.SignInAsync(page, BrowserApiFactory.OwnerEmail, BrowserApiFactory.OwnerPassword);

        // Assert
        await Assertions.Expect(page.Locator(".signed-in-as"))
            .ToHaveTextAsync($"{BrowserApiFactory.OwnerEmail} (Owner)");
    }

    [Fact]
    public async Task Login_WrongPassword_ShowsErrorAndStaysSignedOutAsync()
    {
        // Arrange
        await using IBrowserContext context = await _fixture.NewContextAsync();
        IPage page = await context.NewPageAsync();

        // Act
        await page.GotoAsync($"{_fixture.BaseUrl}/login");
        await page.Locator("#login-email").FillAsync(BrowserApiFactory.OwnerEmail);
        await page.Locator("#login-password").FillAsync("Wrong!Password#1");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).ClickAsync();

        // Assert
        await Assertions.Expect(page.Locator("#login-error")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign out" }))
            .ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Catalog_AnonymousVisitor_IsRedirectedToLoginAsync()
    {
        // Arrange
        await using IBrowserContext context = await _fixture.NewContextAsync();
        IPage page = await context.NewPageAsync();

        // Act
        await page.GotoAsync($"{_fixture.BaseUrl}/catalog/products");

        // Assert
        await page.WaitForURLAsync(new Regex(@"/login\?returnUrl="));
        await Assertions.Expect(page.Locator("#login-email")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task VariantEdit_OwnerChangesFinishAndPrice_PersistsAndShowsSavedAsync()
    {
        // Arrange: the variant is created through the API; the edit is made in the browser.
        SeedApiClient seed = _fixture.Seed;
        Guid productId = await seed.CreateProductAsync(Unique("Edit board"));
        Guid variantId = await seed.CreateVariantAsync(productId, Unique("SKU-EDIT"));

        await using IBrowserContext context = await _fixture.NewContextAsync();
        IPage page = await context.NewPageAsync();
        await _fixture.SignInAsync(page, BrowserApiFactory.OwnerEmail, BrowserApiFactory.OwnerPassword);

        // Act
        await page.GotoAsync($"{_fixture.BaseUrl}/catalog/variants/{variantId}");
        await Assertions.Expect(page.Locator("#variant-finish")).ToBeVisibleAsync();
        await page.Locator("#variant-finish").FillAsync("Satin walnut oil");
        await page.Locator("#variant-retail").FillAsync("59.50");
        await page.Locator("#variant-retail").PressAsync("Tab");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save changes" }).ClickAsync();

        // Assert: the UI confirms, and the server holds the edited values.
        await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new LocatorFilterOptions { HasText = "Saved." }))
            .ToBeVisibleAsync();

        VariantResponse persisted = await seed.GetVariantAsync(variantId);
        Assert.Equal("Satin walnut oil", persisted.Finish);
        Assert.Equal(59.50m, persisted.RetailPrice);
    }

    [Fact]
    public async Task CatalogFlow_NarrowViewport_ListsAndEditsWithoutHorizontalScrollAsync()
    {
        // Arrange
        SeedApiClient seed = _fixture.Seed;
        string productName = Unique("Narrow board");
        string sku = Unique("SKU-NARROW");
        Guid productId = await seed.CreateProductAsync(productName);
        Guid variantId = await seed.CreateVariantAsync(productId, sku);
        // A realistic-length address: the shell shows it in the top bar.
        string staffEmail = $"s{Guid.NewGuid().ToString("N")[..6]}@pp.test";
        await seed.CreateStaffAsync(staffEmail, StaffPassword);

        await using IBrowserContext context = await _fixture.NewContextAsync(NarrowViewportWidth, NarrowViewportHeight);
        IPage page = await context.NewPageAsync();
        await _fixture.SignInAsync(page, staffEmail, StaffPassword);

        // Act + Assert: the product list is a single readable column that fits the screen, and the
        // navigation menu is collapsed behind the toggle until it is opened.
        await page.GotoAsync($"{_fixture.BaseUrl}/catalog/products");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = productName }))
            .ToBeVisibleAsync();
        await AssertNoHorizontalScrollAsync(page);

        ILocator toggle = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Toggle navigation menu" });
        ILocator productsLink = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Products & Stock" });
        await Assertions.Expect(toggle).ToBeVisibleAsync();
        await Assertions.Expect(productsLink).ToBeHiddenAsync();
        await toggle.ClickAsync();
        await Assertions.Expect(productsLink).ToBeVisibleAsync();

        // Move from the product list to the variant list, find the variant by SKU, and open it.
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Variants", Exact = true }).ClickAsync();
        await page.Locator("#variant-search").FillAsync(sku);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Search", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = sku }))
            .ToBeVisibleAsync();
        await AssertNoHorizontalScrollAsync(page);

        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = $"Edit variant {sku}" }).ClickAsync();
        await page.WaitForURLAsync(new Regex($"/catalog/variants/{variantId}$"));
        await Assertions.Expect(page.Locator("#variant-finish")).ToBeVisibleAsync();
        await AssertNoHorizontalScrollAsync(page);

        // The edit still saves from a phone-sized screen.
        await page.Locator("#variant-finish").FillAsync("Phone edit");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save changes" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status).Filter(new LocatorFilterOptions { HasText = "Saved." }))
            .ToBeVisibleAsync();

        Assert.Equal("Phone edit", (await seed.GetVariantAsync(variantId)).Finish);
    }

    private static async Task AssertNoHorizontalScrollAsync(IPage page)
    {
        // Returns an empty string when the page fits; otherwise the widths and the elements that
        // stick out past the right edge, so a failure names the culprit.
        string overflow = await page.EvaluateAsync<string>(
            """
            () => {
                const root = document.documentElement;
                if (root.scrollWidth <= root.clientWidth) { return ""; }
                const culprits = [...document.querySelectorAll("body *")]
                    .filter(e => e.getBoundingClientRect().right > root.clientWidth + 1)
                    .slice(0, 5)
                    .map(e => `${e.tagName.toLowerCase()}${e.id ? "#" + e.id : ""}.${[...e.classList].join(".")}`);
                return `scrollWidth=${root.scrollWidth} clientWidth=${root.clientWidth} wide: ${culprits.join(", ")}`;
            }
            """);
        Assert.True(overflow.Length == 0, $"The page scrolls horizontally at a narrow width: {overflow}");
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}
