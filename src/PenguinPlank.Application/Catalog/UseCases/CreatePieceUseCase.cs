using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Catalog.UseCases;

/// <summary>
/// Creates a <c>ProductPiece</c> (one physical individual) under a serialized variant, enforcing
/// piece-code uniqueness, dimension validity, and the archived-master-data guard, and preserving
/// the care-profile version in effect at production time (requirements R01 §1.6, §1.7, §1.8,
/// §1.12; R10 §2.4, §2.6, §2.7).
/// </summary>
/// <remarks>
/// <para>
/// The use case loads the owning variant's facts (active flag, tracking mode, care profile and its
/// versions, existing piece codes) through the narrow <see cref="ICatalogPieceStore"/>, decides
/// with the <em>pure</em> policies (<see cref="ArchivedMasterDataPolicy"/>,
/// <see cref="PieceCodePolicy"/>, <see cref="DimensionPolicy"/>,
/// <see cref="CareVersionResolver"/>), and persists only on full acceptance. A new piece's public
/// story starts in <see cref="PublicationState.Draft"/> and is never published automatically
/// (requirements 1.12, 2.6, 2.7). The care version is resolved from the versions that existed at
/// the production instant, so a later care edit never changes the piece's preserved version
/// (requirement 2.4). On any rejection the use case returns the policy's failure without persisting
/// (coding-standards §1).
/// </para>
/// </remarks>
public sealed class CreatePieceUseCase
{
    private readonly ICatalogPieceStore _pieceStore;
    private readonly IIdentifierGenerator _identifierGenerator;
    private readonly IAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the use case with its injected boundaries.</summary>
    /// <param name="pieceStore">The piece persistence boundary used to load facts and insert.</param>
    /// <param name="identifierGenerator">The generator for the new piece's identifier.</param>
    /// <param name="auditSink">The audit boundary a successful create records a summary through.</param>
    /// <param name="timeProvider">The injected clock supplying the creation instant and the production-time fallback.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public CreatePieceUseCase(
        ICatalogPieceStore pieceStore,
        IIdentifierGenerator identifierGenerator,
        IAuditSink auditSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(pieceStore);
        ArgumentNullException.ThrowIfNull(identifierGenerator);
        ArgumentNullException.ThrowIfNull(auditSink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _pieceStore = pieceStore;
        _identifierGenerator = identifierGenerator;
        _auditSink = auditSink;
        _timeProvider = timeProvider;
    }

    /// <summary>Executes the create-piece orchestration.</summary>
    /// <param name="request">The piece creation inputs.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A success carrying the new piece's identifier, or a typed failure carrying the rejecting
    /// policy's <see cref="ErrorCode"/> (an archived variant, a duplicate piece code, an invalid
    /// dimension, or an unknown variant).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<Result<Guid>> ExecuteAsync(
        CreatePieceRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        // 1. Validate the piece's own dimensions with the pure policy (no I/O yet).
        Result dimensionResult = ValidateDimensions(request.Dimensions);
        if (dimensionResult.IsFailure)
        {
            return Result.Failure<Guid>(dimensionResult.Error);
        }

        // 2. Load the owning variant's facts and the existing piece codes.
        PieceCreationFacts? facts = await _pieceStore
            .GetPieceCreationFactsAsync(request.VariantId, cancellationToken)
            .ConfigureAwait(false);

        if (facts is null)
        {
            return Result.Failure<Guid>(ErrorCode.Validation, "The owning variant does not exist.");
        }

        // 3. A new piece is a new transaction against the variant: reject if archived.
        Result archivedResult = ArchivedMasterDataPolicy.CanTransact(facts.VariantIsActive);
        if (archivedResult.IsFailure)
        {
            return Result.Failure<Guid>(archivedResult.Error);
        }

        // 4. Decide piece-code uniqueness with the pure policy.
        Result pieceCodeResult = PieceCodePolicy.ValidateUnique(request.PieceCode, facts.ExistingPieceCodes);
        if (pieceCodeResult.IsFailure)
        {
            return Result.Failure<Guid>(pieceCodeResult.Error);
        }

        // 5. Resolve the care-profile version in effect at production time (requirement 2.4).
        DateTimeOffset createdAtUtc = _timeProvider.GetUtcNow();
        DateTimeOffset producedAtUtc = request.ProductionDate ?? createdAtUtc;

        Result<Guid?> careVersionResult = ResolveCareProfileVersionId(facts, producedAtUtc);
        if (careVersionResult.IsFailure)
        {
            return Result.Failure<Guid>(careVersionResult.Error);
        }

        // 6. Every decision accepted: construct the decided record and persist it.
        Guid pieceId = _identifierGenerator.NewId();

        var record = new NewPieceRecord
        {
            PieceId = pieceId,
            VariantId = request.VariantId,
            PublicationState = PublicationPolicy.InitialState,
            CreatedAtUtc = createdAtUtc,
            PieceCode = request.PieceCode,
            Dimensions = request.Dimensions,
            Finish = request.Finish,
            Story = request.Story,
            Status = request.Status,
            ProductionDate = request.ProductionDate,
            CareProfileVersionId = careVersionResult.Value,
        };

        await _pieceStore.InsertPieceAsync(record, cancellationToken).ConfigureAwait(false);

        await _auditSink
            .RecordAsync(
                AuditEntryFactory.CreateEntry(
                    new AuditChange(nameof(ProductPiece), pieceId, AuditOperation.Created),
                    actor,
                    createdAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(pieceId);
    }

    private static Result ValidateDimensions(Dimensions? dimensions)
    {
        if (dimensions is null)
        {
            return Result.Success();
        }

        return DimensionPolicy.Validate(
            dimensions.Length,
            dimensions.Width,
            dimensions.Thickness,
            dimensions.Diameter,
            dimensions.Unit);
    }

    private static Result<Guid?> ResolveCareProfileVersionId(PieceCreationFacts facts, DateTimeOffset producedAtUtc)
    {
        // A variant that references no care profile preserves no version: that is a legitimate
        // absence, not a failure.
        if (facts.CareProfileId is null || facts.CareProfileVersions.Count == 0)
        {
            return Result.Success<Guid?>(null);
        }

        Result<CareProfileVersion> resolved = CareVersionResolver.ResolveForProduction(facts.CareProfileVersions, producedAtUtc);
        if (resolved.IsFailure)
        {
            return Result.Failure<Guid?>(resolved.Error);
        }

        return Result.Success<Guid?>(resolved.Value.Id);
    }
}
