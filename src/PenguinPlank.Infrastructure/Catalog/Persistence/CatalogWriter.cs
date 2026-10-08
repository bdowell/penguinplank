using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Abstractions.Concurrency;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.UseCases;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Infrastructure.Catalog.Persistence;

/// <summary>
/// The Infrastructure implementation of the coarse <see cref="ICatalogWriter"/> mutation boundary.
/// </summary>
/// <remarks>
/// <para>
/// <b>Delegate, do not duplicate.</b> The variant, piece, wood-composition, and archive operations
/// already have single-responsibility use cases (task 8.2) that orchestrate the pure Domain
/// policies over the narrow EF stores (task 8.3). This writer composes those use cases rather than
/// re-implementing their rules, so each business decision stays in exactly one place
/// (coding-standards §1, §3). The product create/update and publish/withdraw operations have no
/// dedicated use case, so this writer performs them directly against the scoped
/// <see cref="PenguinPlankDbContext"/>: it applies the archived guard and loads existing state,
/// then commits a mutation under the client's <c>If-Match</c> token through the
/// <see cref="IConcurrentUpdateExecutor"/> so a stale edit is refused rather than overwriting newer
/// state (requirements 6.5, 6.6).
/// </para>
/// <para>
/// It captures the scoped <see cref="PenguinPlankDbContext"/> and the scoped executor, so it is
/// registered scoped — never a singleton capturing a scoped context (coding-standards §2). Expected
/// business failures (duplicate code, archived record, invalid dimension/proportion, stale version,
/// unknown record) are returned as typed <see cref="Result"/>/<see cref="Result{T}"/> values rather
/// than thrown (coding-standards §6). Time comes from the injected <see cref="TimeProvider"/>, the
/// identifier from the injected <see cref="IIdentifierGenerator"/>, and the actor is passed in — no
/// clock, <c>HttpContext</c>, or EF type leaks across the boundary.
/// </para>
/// </remarks>
public sealed class CatalogWriter : ICatalogWriter
{
    private static readonly string[] s_publicationStateChangeNames = [nameof(Product.PublicationState)];

    private readonly PenguinPlankDbContext _dbContext;
    private readonly IConcurrentUpdateExecutor _concurrentUpdateExecutor;
    private readonly CreateVariantUseCase _createVariantUseCase;
    private readonly CreatePieceUseCase _createPieceUseCase;
    private readonly SetWoodCompositionUseCase _setWoodCompositionUseCase;
    private readonly ArchiveVariantUseCase _archiveVariantUseCase;
    private readonly IIdentifierGenerator _identifierGenerator;
    private readonly IAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the writer with its injected boundaries and use cases.</summary>
    /// <param name="dbContext">The scoped application database context used for product and publication writes.</param>
    /// <param name="concurrentUpdateExecutor">The optimistic-concurrency executor honoring the client's If-Match token.</param>
    /// <param name="createVariantUseCase">The create-variant orchestration.</param>
    /// <param name="createPieceUseCase">The create-piece orchestration.</param>
    /// <param name="setWoodCompositionUseCase">The set-wood-composition orchestration.</param>
    /// <param name="archiveVariantUseCase">The archive/unarchive-variant orchestration.</param>
    /// <param name="identifierGenerator">The generator for new product identifiers.</param>
    /// <param name="auditSink">The audit boundary a successful mutation records a summary through.</param>
    /// <param name="timeProvider">The injected clock supplying mutation instants.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public CatalogWriter(
        PenguinPlankDbContext dbContext,
        IConcurrentUpdateExecutor concurrentUpdateExecutor,
        CreateVariantUseCase createVariantUseCase,
        CreatePieceUseCase createPieceUseCase,
        SetWoodCompositionUseCase setWoodCompositionUseCase,
        ArchiveVariantUseCase archiveVariantUseCase,
        IIdentifierGenerator identifierGenerator,
        IAuditSink auditSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(concurrentUpdateExecutor);
        ArgumentNullException.ThrowIfNull(createVariantUseCase);
        ArgumentNullException.ThrowIfNull(createPieceUseCase);
        ArgumentNullException.ThrowIfNull(setWoodCompositionUseCase);
        ArgumentNullException.ThrowIfNull(archiveVariantUseCase);
        ArgumentNullException.ThrowIfNull(identifierGenerator);
        ArgumentNullException.ThrowIfNull(auditSink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _concurrentUpdateExecutor = concurrentUpdateExecutor;
        _createVariantUseCase = createVariantUseCase;
        _createPieceUseCase = createPieceUseCase;
        _setWoodCompositionUseCase = setWoodCompositionUseCase;
        _archiveVariantUseCase = archiveVariantUseCase;
        _identifierGenerator = identifierGenerator;
        _auditSink = auditSink;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateProductAsync(
        CreateProductRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<Guid>(ErrorCode.Validation, "A product requires a name.");
        }

        Guid productId = _identifierGenerator.NewId();
        DateTimeOffset createdAtUtc = _timeProvider.GetUtcNow();

        var product = new Product
        {
            Id = productId,
            Name = request.Name,
            Category = request.Category,
            PublicDescription = request.PublicDescription,
            InternalNotes = request.InternalNotes,
            PublicationState = PublicationPolicy.InitialState,
            ActiveFlag = true,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RecordAuditAsync(
            new AuditChange(nameof(Product), productId, AuditOperation.Created),
            actor,
            createdAtUtc,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(productId);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateProductAsync(
        UpdateProductRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure(ErrorCode.Validation, "A product requires a name.");
        }

        if (!ETag.TryParse(request.ExpectedVersion, out byte[] expectedRowVersion))
        {
            return StaleVersionFailure();
        }

        Product? product = await _dbContext.Products
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ProductId, cancellationToken)
            .ConfigureAwait(false);

        if (product is null)
        {
            return Result.Failure(ErrorCode.Validation, "The product does not exist.");
        }

        Result archivedResult = ArchivedMasterDataPolicy.CanTransact(product.ActiveFlag);
        if (archivedResult.IsFailure)
        {
            return archivedResult;
        }

        DateTimeOffset updatedAtUtc = _timeProvider.GetUtcNow();

        product.Name = request.Name;
        product.Category = request.Category;
        product.PublicDescription = request.PublicDescription;
        product.InternalNotes = request.InternalNotes;
        product.UpdatedAtUtc = updatedAtUtc;

        Result updateResult = await _concurrentUpdateExecutor
            .ExecuteAsync(product, expectedRowVersion, cancellationToken)
            .ConfigureAwait(false);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await RecordAuditAsync(
            new AuditChange(nameof(Product), product.Id, AuditOperation.Updated),
            actor,
            updatedAtUtc,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc />
    public Task<Result<Guid>> CreateVariantAsync(
        CreateVariantRequest request,
        ActorContext actor,
        CancellationToken cancellationToken) =>
        _createVariantUseCase.ExecuteAsync(request, actor, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> UpdateVariantAsync(
        UpdateVariantRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        if (string.IsNullOrWhiteSpace(request.UnitOfMeasure))
        {
            return Result.Failure(ErrorCode.Validation, "A variant requires a unit of measure.");
        }

        Result dimensionResult = ValidateDimensions(request.Dimensions);
        if (dimensionResult.IsFailure)
        {
            return dimensionResult;
        }

        if (!ETag.TryParse(request.ExpectedVersion, out byte[] expectedRowVersion))
        {
            return StaleVersionFailure();
        }

        ProductVariant? variant = await _dbContext.ProductVariants
            .SingleOrDefaultAsync(candidate => candidate.Id == request.VariantId, cancellationToken)
            .ConfigureAwait(false);

        if (variant is null)
        {
            return Result.Failure(ErrorCode.Validation, "The variant does not exist.");
        }

        Result archivedResult = ArchivedMasterDataPolicy.CanTransact(variant.ActiveFlag);
        if (archivedResult.IsFailure)
        {
            return archivedResult;
        }

        Result barcodeResult = await ValidateBarcodeUniqueAsync(
            request.Barcode,
            request.VariantId,
            cancellationToken).ConfigureAwait(false);
        if (barcodeResult.IsFailure)
        {
            return barcodeResult;
        }

        DateTimeOffset updatedAtUtc = _timeProvider.GetUtcNow();

        variant.Barcode = request.Barcode;
        variant.UnitOfMeasure = request.UnitOfMeasure;
        variant.Length = request.Dimensions?.Length;
        variant.Width = request.Dimensions?.Width;
        variant.Thickness = request.Dimensions?.Thickness;
        variant.Diameter = request.Dimensions?.Diameter;
        variant.DimensionUnit = request.Dimensions?.Unit;
        variant.Finish = request.Finish;
        variant.RetailPrice = request.RetailPrice;
        variant.WholesalePrice = request.WholesalePrice;
        variant.CasePack = request.CasePack;
        variant.CareProfileId = request.CareProfileId;
        variant.UpdatedAtUtc = updatedAtUtc;

        Result updateResult = await _concurrentUpdateExecutor
            .ExecuteAsync(variant, expectedRowVersion, cancellationToken)
            .ConfigureAwait(false);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await RecordAuditAsync(
            new AuditChange(nameof(ProductVariant), variant.Id, AuditOperation.Updated),
            actor,
            updatedAtUtc,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc />
    public Task<Result<Guid>> CreatePieceAsync(
        CreatePieceRequest request,
        ActorContext actor,
        CancellationToken cancellationToken) =>
        _createPieceUseCase.ExecuteAsync(request, actor, cancellationToken);

    /// <inheritdoc />
    public Task<Result> PublishAsync(
        PublicationTransitionRequest request,
        ActorContext actor,
        CancellationToken cancellationToken) =>
        TransitionPublicationAsync(request, PublicationState.PublicApproved, actor, cancellationToken);

    /// <inheritdoc />
    public Task<Result> WithdrawAsync(
        PublicationTransitionRequest request,
        ActorContext actor,
        CancellationToken cancellationToken) =>
        TransitionPublicationAsync(request, PublicationState.Draft, actor, cancellationToken);

    /// <inheritdoc />
    public Task<Result> ArchiveAsync(CatalogRef target, ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetArchivedAsync(target, archive: true, actor, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> UnarchiveAsync(CatalogRef target, ActorContext actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        return SetArchivedAsync(target, archive: false, actor, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> SetWoodCompositionAsync(
        SetWoodCompositionRequest request,
        ActorContext actor,
        CancellationToken cancellationToken) =>
        _setWoodCompositionUseCase.ExecuteAsync(request, actor, cancellationToken);

    private async Task<Result> TransitionPublicationAsync(
        PublicationTransitionRequest request,
        PublicationState targetState,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        if (request.Target.Kind == CatalogEntityKind.Variant)
        {
            // A ProductVariant has no publication state of its own: its public exposure is governed
            // by its owning product (only Product and ProductPiece carry a publication state), so a
            // publish/withdraw against a variant is not a valid operation.
            return Result.Failure(
                ErrorCode.Validation,
                "A variant has no publication state; publish or withdraw its owning product instead.");
        }

        if (!ETag.TryParse(request.ExpectedVersion, out byte[] expectedRowVersion))
        {
            return StaleVersionFailure();
        }

        VersionedEntity? aggregate = await LoadVersionedAggregateAsync(request.Target, cancellationToken)
            .ConfigureAwait(false);

        if (aggregate is null)
        {
            return Result.Failure(ErrorCode.Validation, "The record does not exist.");
        }

        DateTimeOffset updatedAtUtc = _timeProvider.GetUtcNow();
        ApplyPublicationState(aggregate, targetState, updatedAtUtc);

        Result updateResult = await _concurrentUpdateExecutor
            .ExecuteAsync(aggregate, expectedRowVersion, cancellationToken)
            .ConfigureAwait(false);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await RecordAuditAsync(
            new AuditChange(
                EntityTypeName(request.Target.Kind),
                request.Target.Id,
                AuditOperation.Updated,
                s_publicationStateChangeNames),
            actor,
            updatedAtUtc,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result> SetArchivedAsync(
        CatalogRef target,
        bool archive,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // A variant has a dedicated archive/unarchive use case (idempotent, audited): delegate.
        if (target.Kind == CatalogEntityKind.Variant)
        {
            return archive
                ? await _archiveVariantUseCase.ArchiveAsync(target.Id, actor, cancellationToken).ConfigureAwait(false)
                : await _archiveVariantUseCase.UnarchiveAsync(target.Id, actor, cancellationToken).ConfigureAwait(false);
        }

        return await SetProductOrPieceArchivedAsync(target, archive, actor, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> SetProductOrPieceArchivedAsync(
        CatalogRef target,
        bool archive,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        bool desiredActive = !archive;

        if (target.Kind == CatalogEntityKind.Product)
        {
            Product? product = await _dbContext.Products
                .SingleOrDefaultAsync(candidate => candidate.Id == target.Id, cancellationToken)
                .ConfigureAwait(false);

            if (product is null)
            {
                return Result.Failure(ErrorCode.Validation, "The product does not exist.");
            }

            return await ApplyActiveFlagAsync(
                product,
                () => product.ActiveFlag,
                active => product.ActiveFlag = active,
                instant => product.UpdatedAtUtc = instant,
                desiredActive,
                nameof(Product),
                target.Id,
                actor,
                cancellationToken).ConfigureAwait(false);
        }

        ProductPiece? piece = await _dbContext.ProductPieces
            .SingleOrDefaultAsync(candidate => candidate.Id == target.Id, cancellationToken)
            .ConfigureAwait(false);

        if (piece is null)
        {
            return Result.Failure(ErrorCode.Validation, "The piece does not exist.");
        }

        return await ApplyActiveFlagAsync(
            piece,
            () => piece.ActiveFlag,
            active => piece.ActiveFlag = active,
            instant => piece.UpdatedAtUtc = instant,
            desiredActive,
            nameof(ProductPiece),
            target.Id,
            actor,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> ApplyActiveFlagAsync(
        VersionedEntity aggregate,
        Func<bool> readActive,
        Action<bool> writeActive,
        Action<DateTimeOffset> writeUpdatedAt,
        bool desiredActive,
        string entityTypeName,
        Guid entityId,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        if (readActive() == desiredActive)
        {
            // Already in the requested state: a safe, idempotent no-op.
            return Result.Success();
        }

        DateTimeOffset updatedAtUtc = _timeProvider.GetUtcNow();
        writeActive(desiredActive);
        writeUpdatedAt(updatedAtUtc);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RecordAuditAsync(
            new AuditChange(entityTypeName, entityId, AuditOperation.Updated, [nameof(Product.ActiveFlag)]),
            actor,
            updatedAtUtc,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<VersionedEntity?> LoadVersionedAggregateAsync(CatalogRef target, CancellationToken cancellationToken)
    {
        return target.Kind switch
        {
            CatalogEntityKind.Product => await _dbContext.Products
                .SingleOrDefaultAsync(product => product.Id == target.Id, cancellationToken)
                .ConfigureAwait(false),
            CatalogEntityKind.Variant => await _dbContext.ProductVariants
                .SingleOrDefaultAsync(variant => variant.Id == target.Id, cancellationToken)
                .ConfigureAwait(false),
            CatalogEntityKind.Piece => await _dbContext.ProductPieces
                .SingleOrDefaultAsync(piece => piece.Id == target.Id, cancellationToken)
                .ConfigureAwait(false),
            _ => null,
        };
    }

    private static void ApplyPublicationState(VersionedEntity aggregate, PublicationState state, DateTimeOffset updatedAtUtc)
    {
        switch (aggregate)
        {
            case Product product:
                product.PublicationState = state;
                product.UpdatedAtUtc = updatedAtUtc;
                break;
            case ProductPiece piece:
                piece.PublicationState = state;
                piece.UpdatedAtUtc = updatedAtUtc;
                break;
            default:
                // A ProductVariant has no publication state; a variant target is rejected before
                // this point, so only a product or piece reaches here.
                throw new ArgumentOutOfRangeException(
                    nameof(aggregate),
                    aggregate.GetType().Name,
                    "Publication state applies to a product or piece.");
        }
    }

    private async Task<Result> ValidateBarcodeUniqueAsync(
        string? candidateBarcode,
        Guid excludeVariantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(candidateBarcode))
        {
            return Result.Success();
        }

        string normalized = candidateBarcode.Trim();

        bool collision = await _dbContext.ProductVariants
            .AsNoTracking()
            .AnyAsync(
                variant => variant.Id != excludeVariantId
                    && variant.Barcode != null
                    && variant.Barcode == normalized,
                cancellationToken)
            .ConfigureAwait(false);

        return collision
            ? Result.Failure(ErrorCode.Validation, "The barcode matches an existing ProductVariant barcode.")
            : Result.Success();
    }

    private Task RecordAuditAsync(
        AuditChange change,
        ActorContext actor,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken) =>
        _auditSink.RecordAsync(AuditEntryFactory.CreateEntry(change, actor, occurredAtUtc), cancellationToken);

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

    private static Result StaleVersionFailure() =>
        Result.Failure(
            ErrorCode.StaleVersion,
            "The record was changed by someone else since you loaded it. "
                + "Refresh to see the latest version and reapply your changes.");

    private static string EntityTypeName(CatalogEntityKind kind) =>
        kind switch
        {
            CatalogEntityKind.Variant => nameof(ProductVariant),
            CatalogEntityKind.Piece => nameof(ProductPiece),
            _ => nameof(Product),
        };
}
