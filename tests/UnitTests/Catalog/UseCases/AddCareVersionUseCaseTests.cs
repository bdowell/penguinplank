using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Application.Catalog.UseCases;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog.UseCases;

/// <summary>
/// Use-case behavior of <see cref="AddCareVersionUseCase"/> exercised with hand-written controllable
/// substitutes and an injected <see cref="MutableTimeProvider"/> (task 8.4): appending to a profile
/// with no versions yields version 1 (the first-version boundary) and audits it, appending to a
/// profile with existing versions yields the next number, an archived or unknown profile is
/// rejected without appending, and an already-cancelled token short-circuits the operation
/// (requirements R10 Ãƒâ€šÃ‚Â§2.4, Ãƒâ€šÃ‚Â§2.5).
/// </summary>
public sealed class AddCareVersionUseCaseTests
{
    private static readonly Guid s_newVersionId = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid s_careProfileId = new("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly ActorContext s_actor = new(new Guid("12121212-1212-1212-1212-121212121212"), Role.Owner);

    private readonly FakeCareProfileVersionStore _versionStore = new();
    private readonly FakeIdentifierGenerator _identifierGenerator = new(s_newVersionId);
    private readonly RecordingAuditSink _auditSink = new();
    private readonly MutableTimeProvider _timeProvider = new(CatalogUseCaseFakes.FixedNow);

    private AddCareVersionUseCase CreateUseCase() =>
        new(_versionStore, _identifierGenerator, _auditSink, _timeProvider);

    private static AddCareVersionRequest Request(string guidance = "Oil monthly.") =>
        new(s_careProfileId, guidance);

    [Fact]
    public async Task ExecuteAsync_FirstVersionOnActiveProfile_AppendsVersionOneAndAuditsAsync()
    {
        // Arrange: no versions yet ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â the first appended version is numbered 1 (boundary).
        _versionStore.FactsResult = new CareProfileFacts { IsActive = true, CurrentMaxVersionNumber = 0 };
        AddCareVersionUseCase useCase = CreateUseCase();

        // Act
        Result<CareProfileVersionView> result = await useCase.ExecuteAsync(Request("Oil monthly."), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(CareVersionResolver.FirstVersionNumber, result.Value.VersionNumber);
        Assert.Equal(s_newVersionId, result.Value.CareProfileVersionId);
        Assert.Equal("Oil monthly.", result.Value.Guidance);

        NewCareVersionRecord appended = Assert.IsType<NewCareVersionRecord>(_versionStore.AppendedRecord);
        Assert.Equal(1, appended.VersionNumber);
        Assert.Equal(CatalogUseCaseFakes.FixedNow, appended.CreatedAtUtc);

        AuditEntryAssertions.AssertSingleCreated(_auditSink, "CareProfileVersion", s_newVersionId, CatalogUseCaseFakes.FixedNow, s_actor);
    }

    [Fact]
    public async Task ExecuteAsync_ProfileWithExistingVersions_AppendsNextNumberAsync()
    {
        // Arrange: highest existing version is 3, so the next is 4.
        _versionStore.FactsResult = new CareProfileFacts { IsActive = true, CurrentMaxVersionNumber = 3 };
        AddCareVersionUseCase useCase = CreateUseCase();

        // Act
        Result<CareProfileVersionView> result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.VersionNumber);
        Assert.NotNull(_versionStore.AppendedRecord);
        Assert.Equal(4, _versionStore.AppendedRecord!.VersionNumber);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedProfile_RejectsWithoutAppendingAsync()
    {
        // Arrange
        _versionStore.FactsResult = new CareProfileFacts { IsActive = false, CurrentMaxVersionNumber = 1 };
        AddCareVersionUseCase useCase = CreateUseCase();

        // Act
        Result<CareProfileVersionView> result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.ArchivedRecord, result.Error.Code);
        Assert.Null(_versionStore.AppendedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownProfile_RejectsWithoutAppendingAsync()
    {
        // Arrange
        _versionStore.FactsResult = null;
        AddCareVersionUseCase useCase = CreateUseCase();

        // Act
        Result<CareProfileVersionView> result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_versionStore.AppendedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelledToken_ThrowsOperationCanceledAsync()
    {
        // Arrange
        _versionStore.FactsResult = new CareProfileFacts { IsActive = true, CurrentMaxVersionNumber = 0 };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        AddCareVersionUseCase useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(Request(), s_actor, cts.Token));

        Assert.Null(_versionStore.AppendedRecord);
        Assert.Empty(_auditSink.Entries);
    }
}
