using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Application.Catalog.UseCases;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog.UseCases;

/// <summary>
/// Use-case behavior of <see cref="RecordChannelListingUseCase"/> exercised with hand-written
/// controllable substitutes and an injected <see cref="MutableTimeProvider"/> (task 8.4): a listing
/// against an active variant persists the reference record and audits it, each missing required
/// field is rejected without persisting, an archived or unknown variant is rejected, a boundary
/// whitespace-only required field is treated as missing, and an already-cancelled token short-
/// circuits the operation (requirements R01 Ãƒâ€šÃ‚Â§1.8).
/// </summary>
public sealed class RecordChannelListingUseCaseTests
{
    private static readonly Guid s_newListingId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid s_variantId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly ActorContext s_actor = new(new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), Role.Owner);

    private readonly FakeChannelListingStore _listingStore = new();
    private readonly FakeIdentifierGenerator _identifierGenerator = new(s_newListingId);
    private readonly RecordingAuditSink _auditSink = new();
    private readonly MutableTimeProvider _timeProvider = new(CatalogUseCaseFakes.FixedNow);

    private RecordChannelListingUseCase CreateUseCase() =>
        new(_listingStore, _identifierGenerator, _auditSink, _timeProvider);

    private static RecordChannelListingRequest Request(
        string channel = "Etsy",
        string externalId = "listing-1",
        string status = "Active") =>
        new()
        {
            VariantId = s_variantId,
            Channel = channel,
            ExternalId = externalId,
            Status = status,
        };

    [Fact]
    public async Task ExecuteAsync_AllRequiredFieldsOnActiveVariant_PersistsReferenceAndAuditsAsync()
    {
        // Arrange
        _listingStore.VariantActiveResult = true;
        RecordChannelListingUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(s_newListingId, result.Value);

        NewChannelListingRecord inserted = Assert.IsType<NewChannelListingRecord>(_listingStore.InsertedRecord);
        Assert.Equal(s_newListingId, inserted.ChannelListingId);
        Assert.Equal(s_variantId, inserted.VariantId);
        Assert.Equal("Etsy", inserted.Channel);
        Assert.Equal("listing-1", inserted.ExternalId);
        Assert.Equal("Active", inserted.Status);
        Assert.Equal(CatalogUseCaseFakes.FixedNow, inserted.CreatedAtUtc);

        AuditEntryAssertions.AssertSingleCreated(_auditSink, "ChannelListing", s_newListingId, CatalogUseCaseFakes.FixedNow, s_actor);
    }

    [Theory]
    [InlineData("", "listing-1", "Active")]
    [InlineData("Etsy", "", "Active")]
    [InlineData("Etsy", "listing-1", "")]
    public async Task ExecuteAsync_MissingRequiredField_RejectsWithoutPersistingAsync(
        string channel,
        string externalId,
        string status)
    {
        // Arrange
        _listingStore.VariantActiveResult = true;
        RecordChannelListingUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(channel, externalId, status), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_listingStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_WhitespaceOnlyRequiredField_IsTreatedAsMissingAndRejectedAsync()
    {
        // Arrange: a boundary case ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â a required field present only as whitespace counts as absent.
        _listingStore.VariantActiveResult = true;
        RecordChannelListingUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(channel: "   "), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_listingStore.InsertedRecord);
    }

    [Fact]
    public async Task ExecuteAsync_ArchivedVariant_RejectsWithoutPersistingAsync()
    {
        // Arrange
        _listingStore.VariantActiveResult = false;
        RecordChannelListingUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.ArchivedRecord, result.Error.Code);
        Assert.Null(_listingStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownVariant_RejectsWithoutPersistingAsync()
    {
        // Arrange
        _listingStore.VariantActiveResult = null;
        RecordChannelListingUseCase useCase = CreateUseCase();

        // Act
        Result<Guid> result = await useCase.ExecuteAsync(Request(), s_actor, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
        Assert.Null(_listingStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelledToken_ThrowsOperationCanceledAsync()
    {
        // Arrange
        _listingStore.VariantActiveResult = true;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        RecordChannelListingUseCase useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ExecuteAsync(Request(), s_actor, cts.Token));

        Assert.Null(_listingStore.InsertedRecord);
        Assert.Empty(_auditSink.Entries);
    }
}
