using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.IdentityAdministration.Administration;
using PenguinPlank.Domain.Common;
using UnitTests.Media;

namespace UnitTests.IdentityAdministration.Administration;

/// <summary>
/// Use-case behavior of <see cref="CreateStaffUserUseCase"/> exercised with a hand-written
/// staff-user-store substitute and an injected <see cref="MutableTimeProvider"/> (task 9.3): a
/// valid request forwards the actor and the clock-supplied audit instant to the store and returns
/// the created view; missing credentials are rejected without touching the store; and a store-level
/// rejection (duplicate email / policy violation) is surfaced.
/// </summary>
public sealed class CreateStaffUserUseCaseTests
{
    private static readonly ActorContext s_owner = new(new Guid("22222222-2222-2222-2222-222222222222"), Role.Owner);

    private readonly FakeStaffUserStore _store = new();
    private readonly MutableTimeProvider _timeProvider = new(AdministrationFakes.FixedNow);

    private CreateStaffUserUseCase CreateUseCase() => new(_store, _timeProvider);

    private static StaffUserView CreatedView() => new()
    {
        UserId = new Guid("33333333-3333-3333-3333-333333333333"),
        Email = "staff@penguinplank.test",
        Role = Role.Staff,
    };

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ForwardsActorAndAuditInstantAndReturnsViewAsync()
    {
        _store.CreateResult = Result.Success(CreatedView());
        CreateStaffUserUseCase useCase = CreateUseCase();
        var request = new CreateStaffUserRequest("staff@penguinplank.test", "Zr7!qvW2m#Lx", Role.Staff);

        Result<StaffUserView> result = await useCase.ExecuteAsync(request, s_owner, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("staff@penguinplank.test", result.Value.Email);
        Assert.NotNull(_store.Created);
        Assert.Equal(s_owner, _store.Created!.Value.Actor);
        Assert.Equal(AdministrationFakes.FixedNow, _store.Created.Value.AuditedAtUtc);
    }

    [Theory]
    [InlineData(null, "Zr7!qvW2m#Lx")]
    [InlineData("", "Zr7!qvW2m#Lx")]
    [InlineData("staff@penguinplank.test", null)]
    [InlineData("staff@penguinplank.test", "")]
    public async Task ExecuteAsync_MissingCredentials_RejectsWithoutTouchingStoreAsync(string? email, string? password)
    {
        CreateStaffUserUseCase useCase = CreateUseCase();
        var request = new CreateStaffUserRequest(email, password, Role.Staff);

        Result<StaffUserView> result = await useCase.ExecuteAsync(request, s_owner, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_store.Created);
    }

    [Fact]
    public async Task ExecuteAsync_StoreRejectsDuplicateEmail_SurfacesFailureAsync()
    {
        _store.CreateResult = Result.Failure<StaffUserView>(ErrorCode.Validation, "An account with that email already exists.");
        CreateStaffUserUseCase useCase = CreateUseCase();
        var request = new CreateStaffUserRequest("dupe@penguinplank.test", "Zr7!qvW2m#Lx", Role.Staff);

        Result<StaffUserView> result = await useCase.ExecuteAsync(request, s_owner, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelledToken_ThrowsOperationCanceledAsync()
    {
        _store.CreateResult = Result.Success(CreatedView());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        CreateStaffUserUseCase useCase = CreateUseCase();
        var request = new CreateStaffUserRequest("staff@penguinplank.test", "Zr7!qvW2m#Lx", Role.Staff);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(request, s_owner, cts.Token));
    }
}
