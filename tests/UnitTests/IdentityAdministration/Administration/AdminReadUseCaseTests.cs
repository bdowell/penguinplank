using PenguinPlank.Application.Common;
using PenguinPlank.Application.IdentityAdministration.Administration;

namespace UnitTests.IdentityAdministration.Administration;

/// <summary>
/// Use-case behavior of the administration read use cases exercised with hand-written substitutes
/// (task 9.3): <see cref="GetBusinessSettingsUseCase"/> returns the store's view or
/// <see langword="null"/>; <see cref="ListStaffUsersUseCase"/> returns the role-safe staff list; and
/// <see cref="ListAuditEntriesUseCase"/> forwards the query and returns the store's page.
/// </summary>
public sealed class AdminReadUseCaseTests
{
    [Fact]
    public async Task GetBusinessSettings_WhenSeeded_ReturnsViewAsync()
    {
        var store = new FakeBusinessSettingsStore
        {
            GetResult = new BusinessSettingsView
            {
                Timezone = "America/Los_Angeles",
                Currency = "USD",
                DefaultLaborRate = 0m,
                DefaultOverheadRate = 0m,
                DefaultDimensionUnit = "in",
                ImageSizeLimitBytes = 1,
                VideoSizeLimitBytes = 1,
                ETagToken = "t",
            },
        };
        var useCase = new GetBusinessSettingsUseCase(store);

        BusinessSettingsView? view = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal("USD", view!.Currency);
    }

    [Fact]
    public async Task GetBusinessSettings_WhenNotSeeded_ReturnsNullAsync()
    {
        var store = new FakeBusinessSettingsStore { GetResult = null };
        var useCase = new GetBusinessSettingsUseCase(store);

        BusinessSettingsView? view = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Null(view);
    }

    [Fact]
    public async Task ListStaffUsers_ReturnsTheStoresRoleSafeListAsync()
    {
        var store = new FakeStaffUserStore
        {
            ListResult =
            [
                new StaffUserView
                {
                    UserId = new Guid("44444444-4444-4444-4444-444444444444"),
                    Email = "owner@penguinplank.test",
                    Role = PenguinPlank.Application.Abstractions.Role.Owner,
                },
            ],
        };
        var useCase = new ListStaffUsersUseCase(store);

        IReadOnlyList<StaffUserView> result = await useCase.ExecuteAsync(CancellationToken.None);

        StaffUserView only = Assert.Single(result);
        Assert.Equal("owner@penguinplank.test", only.Email);
    }

    [Fact]
    public async Task ListAuditEntries_ForwardsQueryAndReturnsPageAsync()
    {
        var pageRequest = new PageRequest(1, 10);
        var query = new AuditQuery(pageRequest, actorId: new Guid("55555555-5555-5555-5555-555555555555"));
        var reader = new FakeAuditReader
        {
            ListResult = Page.Create<AuditEntryView>(
                [
                    new AuditEntryView
                    {
                        AuditEntryId = Guid.NewGuid(),
                        ActorId = query.ActorId!.Value,
                        Action = "ProductVariantUpdated",
                        EntityType = "ProductVariant",
                        EntityId = Guid.NewGuid(),
                        Timestamp = AdministrationFakes.FixedNow,
                        PermittedChangeSummary = "Updated: SalePrice",
                    },
                ],
                totalCount: 1,
                pageRequest),
        };
        var useCase = new ListAuditEntriesUseCase(reader);

        Page<AuditEntryView> result = await useCase.ExecuteAsync(query, CancellationToken.None);

        Assert.Same(query, reader.ForwardedQuery);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Updated: SalePrice", result.Items[0].PermittedChangeSummary);
    }
}
