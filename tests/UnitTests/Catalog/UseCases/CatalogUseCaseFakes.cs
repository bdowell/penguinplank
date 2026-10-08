using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Auditing;

namespace UnitTests.Catalog.UseCases;

/// <summary>
/// Hand-written controllable substitutes for the narrow persistence boundaries, the audit sink,
/// the identifier generator, and the clock the six catalog use cases depend on (task 8.4).
/// </summary>
/// <remarks>
/// <para>
/// Each store is a focused, responsibility-named fake over exactly the interface its use case
/// depends on (coding-standards §2, §7) — no EF <c>DbSet</c>/<c>IQueryable</c> is mocked, and no
/// live SQL, network, file system, or real clock is involved. The fakes record what the use case
/// decided (the inserted record, the replaced composition, the active-flag flip) so a test asserts
/// <em>observable outcomes</em> rather than private call order. Reads return whatever the test
/// arranged, letting a single harness exercise success, rejection, and boundary paths. Each store
/// honours its <see cref="System.Threading.CancellationToken"/> by throwing
/// <see cref="System.OperationCanceledException"/> when the token is already cancelled, so the
/// cancellation cases observe the use case's I/O boundary behaviour deterministically.
/// </para>
/// </remarks>
internal static class CatalogUseCaseFakes
{
    /// <summary>A fixed, recognizable creation instant the tests assert the use cases stamp from the clock.</summary>
    public static readonly DateTimeOffset FixedNow =
        new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
}

/// <summary>
/// A controllable <see cref="TimeProvider"/> whose current instant the test sets explicitly, so a
/// use case's recorded timestamps are deterministic and assertable (coding-standards §2, §7).
/// </summary>
internal sealed class MutableTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public MutableTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
}

/// <summary>
/// A deterministic <see cref="IIdentifierGenerator"/> that hands back a known identifier, so a
/// test can name the exact id a successful create returns (coding-standards §1, §2).
/// </summary>
internal sealed class FakeIdentifierGenerator : IIdentifierGenerator
{
    private readonly Guid _id;

    public FakeIdentifierGenerator(Guid id) => _id = id;

    /// <summary>The number of times the use case asked for a new identifier.</summary>
    public int CallCount { get; private set; }

    public Guid NewId()
    {
        CallCount++;
        return _id;
    }
}

/// <summary>
/// A recording <see cref="IAuditSink"/> that captures the entries a use case enlists, so a test can
/// assert that an accepted mutation is audited and a rejected one records nothing — without a
/// database (coding-standards §7).
/// </summary>
internal sealed class RecordingAuditSink : IAuditSink
{
    private readonly List<AuditEntry> _entries = [];

    /// <summary>The audit entries the use case enlisted, in order.</summary>
    public IReadOnlyList<AuditEntry> Entries => _entries;

    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.Add(entry);
        return Task.CompletedTask;
    }
}

/// <summary>Controllable substitute for <see cref="ICatalogVariantStore"/>.</summary>
internal sealed class FakeCatalogVariantStore : ICatalogVariantStore
{
    public bool? ProductActiveResult { get; set; }

    public CatalogUniquenessSnapshot UniquenessSnapshot { get; set; } = new()
    {
        ExistingSkus = Array.Empty<string>(),
        ExistingBarcodes = Array.Empty<string>(),
    };

    public VariantFacts? VariantFactsResult { get; set; }

    /// <summary>The variant record the use case decided and inserted, or <see langword="null"/> when none was.</summary>
    public NewVariantRecord? InsertedRecord { get; private set; }

    /// <summary>The last active-flag flip the use case persisted, or <see langword="null"/> when none was.</summary>
    public (Guid VariantId, bool IsActive, DateTimeOffset UpdatedAtUtc)? ActiveFlagChange { get; private set; }

    public Task<CatalogUniquenessSnapshot> GetUniquenessSnapshotAsync(VariantUniquenessQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(UniquenessSnapshot);
    }

    public Task<VariantFacts?> GetVariantFactsAsync(Guid variantId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(VariantFactsResult);
    }

    public Task<bool?> GetProductActiveAsync(Guid productId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ProductActiveResult);
    }

    public Task InsertVariantAsync(NewVariantRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InsertedRecord = record;
        return Task.CompletedTask;
    }

    public Task SetVariantActiveAsync(Guid variantId, bool isActive, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ActiveFlagChange = (variantId, isActive, updatedAtUtc);
        return Task.CompletedTask;
    }
}

/// <summary>Controllable substitute for <see cref="ICatalogPieceStore"/>.</summary>
internal sealed class FakeCatalogPieceStore : ICatalogPieceStore
{
    public PieceCreationFacts? CreationFactsResult { get; set; }

    /// <summary>The piece record the use case decided and inserted, or <see langword="null"/> when none was.</summary>
    public NewPieceRecord? InsertedRecord { get; private set; }

    public Task<PieceCreationFacts?> GetPieceCreationFactsAsync(Guid variantId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreationFactsResult);
    }

    public Task InsertPieceAsync(NewPieceRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InsertedRecord = record;
        return Task.CompletedTask;
    }
}

/// <summary>Controllable substitute for <see cref="ICatalogWoodStore"/>.</summary>
internal sealed class FakeCatalogWoodStore : ICatalogWoodStore
{
    public bool? TargetActiveResult { get; set; }

    /// <summary>The composition replacement the use case persisted, or <see langword="null"/> when none was.</summary>
    public (CatalogRef Target, IReadOnlyList<WoodComponent> Components, DateTimeOffset UpdatedAtUtc)? Replacement { get; private set; }

    public Task<bool?> GetTargetActiveAsync(CatalogRef target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TargetActiveResult);
    }

    public Task ReplaceCompositionAsync(
        CatalogRef target,
        IReadOnlyList<WoodComponent> components,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Replacement = (target, components, updatedAtUtc);
        return Task.CompletedTask;
    }
}

/// <summary>Controllable substitute for <see cref="IChannelListingStore"/>.</summary>
internal sealed class FakeChannelListingStore : IChannelListingStore
{
    public bool? VariantActiveResult { get; set; }

    /// <summary>The listing record the use case decided and inserted, or <see langword="null"/> when none was.</summary>
    public NewChannelListingRecord? InsertedRecord { get; private set; }

    public Task<bool?> GetVariantActiveAsync(Guid variantId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(VariantActiveResult);
    }

    public Task InsertListingAsync(NewChannelListingRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InsertedRecord = record;
        return Task.CompletedTask;
    }
}

/// <summary>Controllable substitute for <see cref="ICareProfileVersionStore"/>.</summary>
internal sealed class FakeCareProfileVersionStore : ICareProfileVersionStore
{
    public CareProfileFacts? FactsResult { get; set; }

    /// <summary>The version record the use case decided and appended, or <see langword="null"/> when none was.</summary>
    public NewCareVersionRecord? AppendedRecord { get; private set; }

    public Task<CareProfileFacts?> GetCareProfileFactsAsync(Guid careProfileId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FactsResult);
    }

    public Task<CareProfileVersionView> AppendVersionAsync(NewCareVersionRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AppendedRecord = record;

        var view = new CareProfileVersionView
        {
            CareProfileVersionId = record.CareProfileVersionId,
            CareProfileId = record.CareProfileId,
            VersionNumber = record.VersionNumber,
            Guidance = record.Guidance,
            CreatedAtUtc = record.CreatedAtUtc,
        };

        return Task.FromResult(view);
    }
}

/// <summary>
/// Shared assertions over the recorded audit trail, so each use-case test asserts the same
/// observable audit outcome (entity type, instance, operation, timestamp, actor) without repeating
/// the shape (coding-standards §7).
/// </summary>
internal static class AuditEntryAssertions
{
    /// <summary>
    /// Asserts the sink recorded exactly one "created" entry for the given entity type and id,
    /// stamped with the supplied instant and actor.
    /// </summary>
    public static void AssertSingleCreated(
        RecordingAuditSink auditSink,
        string entityType,
        Guid entityId,
        DateTimeOffset timestamp,
        ActorContext actor)
    {
        AuditEntry entry = Assert.Single(auditSink.Entries);
        Assert.Equal(entityType, entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(entityType + AuditOperation.Created, entry.Action);
        Assert.Equal(timestamp, entry.Timestamp);
        Assert.Equal(actor.UserId, entry.ActorId);
    }

    /// <summary>
    /// Asserts the sink recorded exactly one "updated" entry for the given entity type and id,
    /// stamped with the supplied instant and actor.
    /// </summary>
    public static void AssertSingleUpdated(
        RecordingAuditSink auditSink,
        string entityType,
        Guid entityId,
        DateTimeOffset timestamp,
        ActorContext actor)
    {
        AuditEntry entry = Assert.Single(auditSink.Entries);
        Assert.Equal(entityType, entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(entityType + AuditOperation.Updated, entry.Action);
        Assert.Equal(timestamp, entry.Timestamp);
        Assert.Equal(actor.UserId, entry.ActorId);
    }
}
