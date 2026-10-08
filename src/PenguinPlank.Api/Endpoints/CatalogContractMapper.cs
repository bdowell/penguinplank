using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Common;
using PenguinPlank.Contracts.Catalog;
using PenguinPlank.Contracts.Common;
using PenguinPlank.Domain.Catalog;
using AppDimensions = PenguinPlank.Application.Catalog.Dimensions;
using AppWoodComponent = PenguinPlank.Application.Catalog.WoodComponent;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// The pure, directly testable mapping between the Application layer's catalog read models and
/// request records and the versioned Contracts DTOs (coding-standards §1, §3).
/// </summary>
/// <remarks>
/// <para>
/// Keeping this translation in one pure static type means the endpoints stay thin and the
/// non-trivial parts — the stable string ↔ enum conversions for tracking mode and publication
/// state, the dimensions and wood-composition shape mapping, and the paged-envelope mapping — can
/// be exercised with ordinary values without starting the application. No <c>IQueryable</c>,
/// <c>DbContext</c>, EF entity, or <c>HttpContext</c> is touched here; the API never serializes a
/// persistence graph, only these DTOs (requirement A8 §10.3; design "API Design").
/// </para>
/// <para>
/// Prices are reported with the explicit Phase A currency (<see cref="Currency"/> = USD). The
/// invalid-string cases on the request-side enum parsers surface as a typed validation failure at
/// the endpoint rather than an exception, because they reflect a malformed caller request.
/// </para>
/// </remarks>
public static class CatalogContractMapper
{
    /// <summary>The ISO 4217 currency code Phase A prices are expressed in.</summary>
    public const string Currency = "USD";

    /// <summary>Maps a product read model to its response DTO.</summary>
    /// <param name="view">The Application product read model.</param>
    /// <returns>The versioned product response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="view"/> is <see langword="null"/>.</exception>
    public static ProductResponse ToResponse(ProductView view)
    {
        System.ArgumentNullException.ThrowIfNull(view);

        return new ProductResponse
        {
            ProductId = view.ProductId,
            Name = view.Name,
            Category = view.Category,
            PublicDescription = view.PublicDescription,
            InternalNotes = view.InternalNotes,
            PublicationState = view.PublicationState.ToString(),
            IsActive = view.IsActive,
            ETag = view.ETagToken,
            CreatedAt = view.CreatedAtUtc,
            UpdatedAt = view.UpdatedAtUtc,
        };
    }

    /// <summary>Maps a variant read model to its response DTO.</summary>
    /// <param name="view">The Application variant read model.</param>
    /// <returns>The versioned variant response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="view"/> is <see langword="null"/>.</exception>
    public static VariantResponse ToResponse(ProductVariantView view)
    {
        System.ArgumentNullException.ThrowIfNull(view);

        return new VariantResponse
        {
            VariantId = view.VariantId,
            ProductId = view.ProductId,
            Sku = view.Sku,
            Barcode = view.Barcode,
            TrackingMode = view.TrackingMode.ToString(),
            UnitOfMeasure = view.UnitOfMeasure,
            Dimensions = ToContract(view.Dimensions),
            Finish = view.Finish,
            RetailPrice = view.RetailPrice,
            WholesalePrice = view.WholesalePrice,
            Currency = Currency,
            CasePack = view.CasePack,
            CareProfileId = view.CareProfileId,
            WoodComposition = [.. view.WoodComposition.Select(ToContract)],
            IsActive = view.IsActive,
            ETag = view.ETagToken,
        };
    }

    /// <summary>
    /// Maps a <b>role-projected</b> variant view to its response DTO. This is the overload the
    /// <c>/api/v1/variants</c> endpoints use so a Staff response is built from the role-safe base
    /// shape and never from an Owner-only one (requirement A2 §5.10, §5.11).
    /// </summary>
    /// <param name="view">
    /// The projected variant view produced by <c>CatalogVariantResponseProjector</c> for the
    /// authenticated actor's role. For Staff it is the role-safe base
    /// <see cref="PenguinPlank.Application.Catalog.Projection.CatalogVariantResponseView"/>; for an
    /// Owner it is the derived Owner shape. Either way this mapper only reads the members declared
    /// on the base, so it can never surface an Owner-only field to a Staff caller.
    /// </param>
    /// <returns>The versioned variant response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="view"/> is <see langword="null"/>.</exception>
    public static VariantResponse ToResponse(
        PenguinPlank.Application.Catalog.Projection.CatalogVariantResponseView view)
    {
        System.ArgumentNullException.ThrowIfNull(view);

        return new VariantResponse
        {
            VariantId = view.VariantId,
            ProductId = view.ProductId,
            Sku = view.Sku,
            Barcode = view.Barcode,
            TrackingMode = view.TrackingMode.ToString(),
            UnitOfMeasure = view.UnitOfMeasure,
            Dimensions = ToContract(view.Dimensions),
            Finish = view.Finish,
            RetailPrice = view.RetailPrice,
            WholesalePrice = view.WholesalePrice,
            Currency = Currency,
            CasePack = view.CasePack,
            CareProfileId = view.CareProfileId,
            WoodComposition = [.. view.WoodComposition.Select(ToContract)],
            IsActive = view.IsActive,
            ETag = view.ETagToken,
        };
    }

    /// <summary>Maps a piece read model to its response DTO.</summary>
    /// <param name="view">The Application piece read model.</param>
    /// <returns>The versioned piece response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="view"/> is <see langword="null"/>.</exception>
    public static PieceResponse ToResponse(ProductPieceView view)
    {
        System.ArgumentNullException.ThrowIfNull(view);

        return new PieceResponse
        {
            PieceId = view.PieceId,
            VariantId = view.VariantId,
            PieceCode = view.PieceCode,
            Dimensions = ToContract(view.Dimensions),
            Finish = view.Finish,
            Story = view.Story,
            Status = view.Status,
            ProductionDate = view.ProductionDate,
            CareProfileVersionId = view.CareProfileVersionId,
            PublicationState = view.PublicationState.ToString(),
            IsActive = view.IsActive,
            ETag = view.ETagToken,
        };
    }

    /// <summary>Maps a care-profile version read model to its response DTO.</summary>
    /// <param name="view">The Application care-profile version read model.</param>
    /// <returns>The versioned care-profile version response.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="view"/> is <see langword="null"/>.</exception>
    public static CareProfileVersionResponse ToResponse(CareProfileVersionView view)
    {
        System.ArgumentNullException.ThrowIfNull(view);

        return new CareProfileVersionResponse
        {
            CareProfileVersionId = view.CareProfileVersionId,
            CareProfileId = view.CareProfileId,
            VersionNumber = view.VersionNumber,
            Guidance = view.Guidance,
            CreatedAt = view.CreatedAtUtc,
        };
    }

    /// <summary>
    /// Maps a materialized <see cref="Page{T}"/> read model into the versioned paged-response
    /// envelope, projecting each item through <paramref name="itemMapper"/>.
    /// </summary>
    /// <typeparam name="TView">The Application read-model item type.</typeparam>
    /// <typeparam name="TContract">The Contracts item DTO type.</typeparam>
    /// <param name="page">The materialized page.</param>
    /// <param name="itemMapper">The per-item view-to-DTO projection.</param>
    /// <returns>The versioned paged response carrying the mapped items and paging context.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="page"/> or <paramref name="itemMapper"/> is <see langword="null"/>.</exception>
    public static PagedResponse<TContract> ToPagedResponse<TView, TContract>(
        Page<TView> page,
        System.Func<TView, TContract> itemMapper)
    {
        System.ArgumentNullException.ThrowIfNull(page);
        System.ArgumentNullException.ThrowIfNull(itemMapper);

        return new PagedResponse<TContract>
        {
            Items = [.. page.Items.Select(itemMapper)],
            TotalCount = page.TotalCount,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
        };
    }

    /// <summary>Maps the create-product request DTO to the Application request record.</summary>
    /// <param name="contract">The create-product request body.</param>
    /// <returns>The Application create-product request.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="contract"/> is <see langword="null"/>.</exception>
    public static CreateProductRequest ToRequest(CreateProductContract contract)
    {
        System.ArgumentNullException.ThrowIfNull(contract);

        return new CreateProductRequest
        {
            Name = contract.Name,
            Category = contract.Category,
            PublicDescription = contract.PublicDescription,
            InternalNotes = contract.InternalNotes,
        };
    }

    /// <summary>Maps the update-product request DTO (plus route id and If-Match token) to the Application request record.</summary>
    /// <param name="contract">The update-product request body.</param>
    /// <param name="productId">The product identifier from the route.</param>
    /// <param name="expectedVersion">The concurrency token from the <c>If-Match</c> header.</param>
    /// <returns>The Application update-product request.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="contract"/> is <see langword="null"/>.</exception>
    public static UpdateProductRequest ToRequest(UpdateProductContract contract, Guid productId, string expectedVersion)
    {
        System.ArgumentNullException.ThrowIfNull(contract);

        return new UpdateProductRequest
        {
            ProductId = productId,
            ExpectedVersion = expectedVersion,
            Name = contract.Name,
            Category = contract.Category,
            PublicDescription = contract.PublicDescription,
            InternalNotes = contract.InternalNotes,
        };
    }

    /// <summary>Maps the create-variant request DTO to the Application request record.</summary>
    /// <param name="contract">The create-variant request body.</param>
    /// <returns>The Application create-variant request.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="contract"/> is <see langword="null"/>.</exception>
    /// <exception cref="CatalogContractFormatException">Thrown when <see cref="CreateVariantContract.TrackingMode"/> is not a recognized tracking mode.</exception>
    public static CreateVariantRequest ToRequest(CreateVariantContract contract)
    {
        System.ArgumentNullException.ThrowIfNull(contract);

        return new CreateVariantRequest
        {
            ProductId = contract.ProductId,
            Sku = contract.Sku,
            TrackingMode = ParseTrackingMode(contract.TrackingMode),
            UnitOfMeasure = contract.UnitOfMeasure,
            Barcode = contract.Barcode,
            Dimensions = ToRequest(contract.Dimensions),
            Finish = contract.Finish,
            RetailPrice = contract.RetailPrice,
            WholesalePrice = contract.WholesalePrice,
            CasePack = contract.CasePack,
            CareProfileId = contract.CareProfileId,
            WoodComposition = [.. contract.WoodComposition.Select(ToRequest)],
        };
    }

    /// <summary>Maps the update-variant request DTO (plus route id and If-Match token) to the Application request record.</summary>
    /// <param name="contract">The update-variant request body.</param>
    /// <param name="variantId">The variant identifier from the route.</param>
    /// <param name="expectedVersion">The concurrency token from the <c>If-Match</c> header.</param>
    /// <returns>The Application update-variant request.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="contract"/> is <see langword="null"/>.</exception>
    public static UpdateVariantRequest ToRequest(UpdateVariantContract contract, Guid variantId, string expectedVersion)
    {
        System.ArgumentNullException.ThrowIfNull(contract);

        return new UpdateVariantRequest
        {
            VariantId = variantId,
            ExpectedVersion = expectedVersion,
            Barcode = contract.Barcode,
            UnitOfMeasure = contract.UnitOfMeasure,
            Dimensions = ToRequest(contract.Dimensions),
            Finish = contract.Finish,
            RetailPrice = contract.RetailPrice,
            WholesalePrice = contract.WholesalePrice,
            CasePack = contract.CasePack,
            CareProfileId = contract.CareProfileId,
        };
    }

    /// <summary>Maps the create-piece request DTO to the Application request record.</summary>
    /// <param name="contract">The create-piece request body.</param>
    /// <returns>The Application create-piece request.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="contract"/> is <see langword="null"/>.</exception>
    public static CreatePieceRequest ToRequest(CreatePieceContract contract)
    {
        System.ArgumentNullException.ThrowIfNull(contract);

        return new CreatePieceRequest
        {
            VariantId = contract.VariantId,
            PieceCode = contract.PieceCode,
            Dimensions = ToRequest(contract.Dimensions),
            Finish = contract.Finish,
            Story = contract.Story,
            Status = contract.Status,
            ProductionDate = contract.ProductionDate,
        };
    }

    /// <summary>
    /// Parses a stable tracking-mode string into the domain <see cref="TrackingMode"/>.
    /// </summary>
    /// <param name="value">The tracking-mode string ("Serialized" or "Quantity"), case-insensitive.</param>
    /// <returns>The parsed <see cref="TrackingMode"/>.</returns>
    /// <exception cref="CatalogContractFormatException">Thrown when <paramref name="value"/> is not a recognized tracking mode.</exception>
    public static TrackingMode ParseTrackingMode(string value)
    {
        if (System.Enum.TryParse(value, ignoreCase: true, out TrackingMode mode) && System.Enum.IsDefined(mode))
        {
            return mode;
        }

        throw new CatalogContractFormatException(
            $"'{value}' is not a recognized tracking mode. Expected 'Serialized' or 'Quantity'.");
    }

    private static AppDimensions? ToRequest(DimensionsContract? contract)
    {
        if (contract is null)
        {
            return null;
        }

        return new AppDimensions
        {
            Length = contract.Length,
            Width = contract.Width,
            Thickness = contract.Thickness,
            Diameter = contract.Diameter,
            Unit = contract.Unit,
        };
    }

    private static DimensionsContract? ToContract(AppDimensions? dimensions)
    {
        if (dimensions is null)
        {
            return null;
        }

        return new DimensionsContract
        {
            Length = dimensions.Length,
            Width = dimensions.Width,
            Thickness = dimensions.Thickness,
            Diameter = dimensions.Diameter,
            Unit = dimensions.Unit,
        };
    }

    private static AppWoodComponent ToRequest(WoodComponentContract contract)
    {
        System.ArgumentNullException.ThrowIfNull(contract);
        return new AppWoodComponent(contract.WoodSpeciesId, contract.Proportion);
    }

    private static WoodComponentContract ToContract(AppWoodComponent component)
    {
        return new WoodComponentContract
        {
            WoodSpeciesId = component.WoodSpeciesId,
            Proportion = component.Proportion,
        };
    }
}
