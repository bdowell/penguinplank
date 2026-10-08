using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Catalog.Persistence;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Application.Catalog.UseCases;

/// <summary>
/// Creates a <c>ProductVariant</c> under an existing active product, enforcing SKU and barcode
/// uniqueness, dimension validity, and wood-proportion validity through the pure Domain policies
/// before persisting (requirements R01 §1.1–§1.5, §1.8, §1.12).
/// </summary>
/// <remarks>
/// <para>
/// This use case <em>sequences</em> the catalog write: validate inputs, load the existing-state
/// facts (the owning product's active flag and the existing SKUs/barcodes) through the narrow
/// <see cref="ICatalogVariantStore"/>, decide with the <em>pure</em> policies
/// (<see cref="ArchivedMasterDataPolicy"/>, <see cref="SkuPolicy"/>,
/// <see cref="DimensionPolicy"/>, <see cref="WoodCompositionPolicy"/>), and only when every
/// decision accepts does it persist and record an audit summary. When a policy rejects, the use
/// case returns the policy's typed <see cref="Result{T}"/> failure <b>without persisting anything</b>
/// (coding-standards §1). It holds no EF type and reads no <c>HttpContext</c>; every dependency
/// arrives by constructor injection, time comes from the injected <see cref="TimeProvider"/>, and
/// the actor is passed in (coding-standards §2, §3).
/// </para>
/// </remarks>
public sealed class CreateVariantUseCase
{
    private readonly ICatalogVariantStore _variantStore;
    private readonly IIdentifierGenerator _identifierGenerator;
    private readonly IAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the use case with its injected boundaries.</summary>
    /// <param name="variantStore">The variant persistence boundary used to load facts and insert.</param>
    /// <param name="identifierGenerator">The generator for the new variant's identifier.</param>
    /// <param name="auditSink">The audit boundary a successful create records a summary through.</param>
    /// <param name="timeProvider">The injected clock supplying the creation instant.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is <see langword="null"/>.</exception>
    public CreateVariantUseCase(
        ICatalogVariantStore variantStore,
        IIdentifierGenerator identifierGenerator,
        IAuditSink auditSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(variantStore);
        ArgumentNullException.ThrowIfNull(identifierGenerator);
        ArgumentNullException.ThrowIfNull(auditSink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _variantStore = variantStore;
        _identifierGenerator = identifierGenerator;
        _auditSink = auditSink;
        _timeProvider = timeProvider;
    }

    /// <summary>Executes the create-variant orchestration.</summary>
    /// <param name="request">The variant creation inputs.</param>
    /// <param name="actor">The authenticated actor performing the mutation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A success carrying the new variant's identifier, or a typed failure carrying the rejecting
    /// policy's <see cref="ErrorCode"/> (duplicate SKU/barcode, invalid dimension or proportion,
    /// archived product, or an unknown product).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> or <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<Result<Guid>> ExecuteAsync(
        CreateVariantRequest request,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        // 1. Validate inputs with the pure dimension and wood policies (no I/O yet).
        Result dimensionResult = ValidateDimensions(request.Dimensions);
        if (dimensionResult.IsFailure)
        {
            return Result.Failure<Guid>(dimensionResult.Error);
        }

        Result woodResult = WoodCompositionPolicy.Validate(request.WoodComposition.Select(component => component.Proportion));
        if (woodResult.IsFailure)
        {
            return Result.Failure<Guid>(woodResult.Error);
        }

        // 2. Load existing-state facts: the owning product must exist and be active.
        bool? productActive = await _variantStore
            .GetProductActiveAsync(request.ProductId, cancellationToken)
            .ConfigureAwait(false);

        if (productActive is null)
        {
            return Result.Failure<Guid>(
                ErrorCode.Validation,
                "The owning product does not exist.");
        }

        Result archivedResult = ArchivedMasterDataPolicy.CanTransact(productActive.Value);
        if (archivedResult.IsFailure)
        {
            return Result.Failure<Guid>(archivedResult.Error);
        }

        // 3. Load existing codes and decide SKU/barcode uniqueness with the pure policies.
        CatalogUniquenessSnapshot snapshot = await _variantStore
            .GetUniquenessSnapshotAsync(
                new VariantUniquenessQuery
                {
                    CandidateSku = request.Sku,
                    CandidateBarcode = request.Barcode,
                    ExcludeVariantId = null,
                },
                cancellationToken)
            .ConfigureAwait(false);

        Result skuResult = SkuPolicy.ValidateUnique(request.Sku, snapshot.ExistingSkus);
        if (skuResult.IsFailure)
        {
            return Result.Failure<Guid>(skuResult.Error);
        }

        Result barcodeResult = ValidateBarcodeUnique(request.Barcode, snapshot.ExistingBarcodes);
        if (barcodeResult.IsFailure)
        {
            return Result.Failure<Guid>(barcodeResult.Error);
        }

        // 4. Every decision accepted: construct the decided record and persist it.
        Guid variantId = _identifierGenerator.NewId();
        DateTimeOffset createdAtUtc = _timeProvider.GetUtcNow();

        var record = new NewVariantRecord
        {
            VariantId = variantId,
            ProductId = request.ProductId,
            Sku = request.Sku,
            TrackingMode = request.TrackingMode,
            UnitOfMeasure = request.UnitOfMeasure,
            PublicationState = PublicationPolicy.InitialState,
            CreatedAtUtc = createdAtUtc,
            Barcode = request.Barcode,
            Dimensions = request.Dimensions,
            Finish = request.Finish,
            RetailPrice = request.RetailPrice,
            WholesalePrice = request.WholesalePrice,
            CasePack = request.CasePack,
            CareProfileId = request.CareProfileId,
            WoodComposition = request.WoodComposition,
        };

        await _variantStore.InsertVariantAsync(record, cancellationToken).ConfigureAwait(false);

        // 5. Record a domain-significant audit summary in the same unit of work.
        await _auditSink
            .RecordAsync(
                AuditEntryFactory.CreateEntry(
                    new AuditChange(nameof(ProductVariant), variantId, AuditOperation.Created),
                    actor,
                    createdAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(variantId);
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

    private static Result ValidateBarcodeUnique(string? candidateBarcode, IReadOnlyCollection<string> existingBarcodes)
    {
        if (string.IsNullOrWhiteSpace(candidateBarcode))
        {
            return Result.Success();
        }

        string normalizedCandidate = candidateBarcode.Trim();

        foreach (string existingBarcode in existingBarcodes)
        {
            if (!string.IsNullOrWhiteSpace(existingBarcode)
                && string.Equals(existingBarcode.Trim(), normalizedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure(
                    ErrorCode.Validation,
                    "The barcode matches an existing ProductVariant barcode.");
            }
        }

        return Result.Success();
    }
}
