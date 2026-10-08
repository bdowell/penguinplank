using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.IdentityAdministration.Administration;
using PenguinPlank.Domain.Common;
using UnitTests.Media;

namespace UnitTests.IdentityAdministration.Administration;

/// <summary>
/// Use-case behavior of <see cref="UpdateBusinessSettingsUseCase"/> exercised with a hand-written
/// settings-store substitute and an injected <see cref="MutableTimeProvider"/> (task 9.3): a valid
/// edit stamps the clock-supplied instant and returns the updated view; malformed inputs are
/// rejected without touching the store; a stale <c>If-Match</c> surfaces the store's
/// <see cref="ErrorCode.StaleVersion"/>; and an already-cancelled token short-circuits.
/// </summary>
public sealed class UpdateBusinessSettingsUseCaseTests
{
    private static readonly ActorContext s_owner = new(new Guid("11111111-1111-1111-1111-111111111111"), Role.Owner);

    private readonly FakeBusinessSettingsStore _store = new();
    private readonly MutableTimeProvider _timeProvider = new(AdministrationFakes.FixedNow);

    private UpdateBusinessSettingsUseCase CreateUseCase() => new(_store, _timeProvider);

    private static UpdateBusinessSettingsRequest ValidRequest(string expectedVersion = "\"token\"") => new()
    {
        Timezone = "America/Los_Angeles",
        Currency = "USD",
        DefaultLaborRate = 25.000000m,
        DefaultOverheadRate = 10.000000m,
        DefaultDimensionUnit = "in",
        ImageSizeLimitBytes = 20L * 1024 * 1024,
        VideoSizeLimitBytes = 200L * 1024 * 1024,
        ExpectedVersion = expectedVersion,
    };

    private static BusinessSettingsView UpdatedView() => new()
    {
        Timezone = "America/Los_Angeles",
        Currency = "USD",
        DefaultLaborRate = 25.000000m,
        DefaultOverheadRate = 10.000000m,
        DefaultDimensionUnit = "in",
        ImageSizeLimitBytes = 20L * 1024 * 1024,
        VideoSizeLimitBytes = 200L * 1024 * 1024,
        ETagToken = "new-token",
    };

    [Fact]
    public async Task ExecuteAsync_ValidRequest_StampsInstantAndReturnsUpdatedViewAsync()
    {
        _store.UpdateResult = Result.Success(UpdatedView());
        UpdateBusinessSettingsUseCase useCase = CreateUseCase();

        Result<BusinessSettingsView> result = await useCase.ExecuteAsync(ValidRequest(), s_owner, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-token", result.Value.ETagToken);
        Assert.NotNull(_store.AppliedUpdate);
        Assert.Equal(AdministrationFakes.FixedNow, _store.AppliedUpdate!.Value.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("", "USD", "in")]
    [InlineData("America/Los_Angeles", "", "in")]
    [InlineData("America/Los_Angeles", "USD", "")]
    public async Task ExecuteAsync_MissingRequiredField_RejectsWithoutTouchingStoreAsync(
        string timezone,
        string currency,
        string dimensionUnit)
    {
        UpdateBusinessSettingsUseCase useCase = CreateUseCase();
        UpdateBusinessSettingsRequest request = ValidRequest() with
        {
            Timezone = timezone,
            Currency = currency,
            DefaultDimensionUnit = dimensionUnit,
        };

        Result<BusinessSettingsView> result = await useCase.ExecuteAsync(request, s_owner, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_store.AppliedUpdate);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeRate_RejectsWithoutTouchingStoreAsync()
    {
        UpdateBusinessSettingsUseCase useCase = CreateUseCase();
        UpdateBusinessSettingsRequest request = ValidRequest() with { DefaultLaborRate = -1m };

        Result<BusinessSettingsView> result = await useCase.ExecuteAsync(request, s_owner, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_store.AppliedUpdate);
    }

    [Fact]
    public async Task ExecuteAsync_StaleIfMatch_SurfacesStaleVersionFailureAsync()
    {
        _store.UpdateResult = Result.Failure<BusinessSettingsView>(ErrorCode.StaleVersion, "stale");
        UpdateBusinessSettingsUseCase useCase = CreateUseCase();

        Result<BusinessSettingsView> result = await useCase.ExecuteAsync(ValidRequest(), s_owner, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.StaleVersion, result.Error.Code);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelledToken_ThrowsOperationCanceledAsync()
    {
        _store.UpdateResult = Result.Success(UpdatedView());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        UpdateBusinessSettingsUseCase useCase = CreateUseCase();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(ValidRequest(), s_owner, cts.Token));
    }
}
