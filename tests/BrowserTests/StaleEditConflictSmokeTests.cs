using BrowserTests.Hosting;
using Microsoft.Playwright;

namespace BrowserTests;

/// <summary>
/// Acceptance scenario #13 end to end: two signed-in sessions edit the same variant; the second
/// save is refused with a conflict and a refresh prompt, and the first session's change is never
/// silently overwritten (requirements A5 7.6, 7.7).
/// </summary>
[Collection(BrowserWorkflowTestGroup.Name)]
public sealed class StaleEditConflictSmokeTests
{
    private const string StaffPassword = "Staff!Browser#2025";
    private const string StaffFinish = "Finish saved by staff";
    private const string OwnerFinish = "Finish typed by owner";
    private const string ConflictText = "changed by someone else";
    private const string SavingText = "Saving…";

    private readonly BrowserWorkflowFixture _fixture;

    public StaleEditConflictSmokeTests(BrowserWorkflowFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task VariantEdit_TwoSessionsEditSameRecord_SecondGetsConflictAndNoSilentOverwriteAsync()
    {
        // Arrange: one variant, opened by two independent signed-in sessions (Owner and Staff).
        SeedApiClient seed = _fixture.Seed;
        Guid productId = await seed.CreateProductAsync($"Conflict board {Guid.NewGuid().ToString("N")[..8]}");
        Guid variantId = await seed.CreateVariantAsync(productId, $"SKU-CONFLICT-{Guid.NewGuid().ToString("N")[..8]}");
        string staffEmail = $"conflict-{Guid.NewGuid():N}@penguinplank.test";
        await seed.CreateStaffAsync(staffEmail, StaffPassword);

        await using IBrowserContext ownerContext = await _fixture.NewContextAsync();
        await using IBrowserContext staffContext = await _fixture.NewContextAsync();
        IPage ownerPage = await ownerContext.NewPageAsync();
        IPage staffPage = await staffContext.NewPageAsync();
        await _fixture.SignInAsync(ownerPage, BrowserApiFactory.OwnerEmail, BrowserApiFactory.OwnerPassword);
        await _fixture.SignInAsync(staffPage, staffEmail, StaffPassword);

        await OpenVariantAsync(ownerPage, variantId);
        await OpenVariantAsync(staffPage, variantId);

        // Act 1: Staff saves first, which advances the record's version.
        await EditFinishAsync(staffPage, StaffFinish);
        await Assertions.Expect(SavedStatus(staffPage)).ToBeVisibleAsync();

        // Act 2: the Owner, still holding the old version, saves a different change.
        await EditFinishAsync(ownerPage, OwnerFinish);

        // Assert: the Owner sees a conflict with a refresh prompt and cannot resubmit blindly. The
        // banner text is read first so a failure reports what the page showed instead.
        ILocator banner = ownerPage.Locator(".cp-banner-text");
        await Assertions.Expect(banner).ToBeVisibleAsync();
        await Assertions.Expect(banner).Not.ToHaveTextAsync(SavingText);
        Assert.Contains(ConflictText, await banner.InnerTextAsync(), StringComparison.Ordinal);

        ILocator conflict = ownerPage.GetByRole(AriaRole.Alert).Filter(new LocatorFilterOptions { HasText = ConflictText });
        await Assertions.Expect(conflict).ToBeVisibleAsync();
        ILocator reload = ownerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Reload latest version" });
        await Assertions.Expect(reload).ToBeVisibleAsync();
        await Assertions.Expect(SaveButton(ownerPage)).ToBeDisabledAsync();

        // Assert: nothing was overwritten; the server still holds the Staff change.
        Assert.Equal(StaffFinish, (await seed.GetVariantAsync(variantId)).Finish);

        // Act 3: the Owner refreshes, sees the latest value, and re-applies the change on top of it.
        await reload.ClickAsync();
        await Assertions.Expect(ownerPage.Locator("#variant-finish")).ToHaveValueAsync(StaffFinish);
        await Assertions.Expect(conflict).ToHaveCountAsync(0);

        await EditFinishAsync(ownerPage, OwnerFinish);

        // Assert: the deliberate, up-to-date edit now succeeds.
        await Assertions.Expect(SavedStatus(ownerPage)).ToBeVisibleAsync();
        Assert.Equal(OwnerFinish, (await seed.GetVariantAsync(variantId)).Finish);
    }

    private async Task OpenVariantAsync(IPage page, Guid variantId)
    {
        await page.GotoAsync($"{_fixture.BaseUrl}/catalog/variants/{variantId}");
        await Assertions.Expect(page.Locator("#variant-finish")).ToBeVisibleAsync();
    }

    private static async Task EditFinishAsync(IPage page, string finish)
    {
        await page.Locator("#variant-finish").FillAsync(finish);
        await SaveButton(page).ClickAsync();
    }

    private static ILocator SaveButton(IPage page) =>
        page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save changes" });

    private static ILocator SavedStatus(IPage page) =>
        page.GetByRole(AriaRole.Status).Filter(new LocatorFilterOptions { HasText = "Saved." });
}
