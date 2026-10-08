using System.Net;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Media;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Acceptance;

/// <summary>
/// Acceptance scenario #11 (requirement A5 §7.4): an unauthenticated caller is denied private
/// product, media, and catalog data <b>regardless of publication metadata</b> — a public-approved
/// resource is still refused anonymously, because publication/visibility is eligibility metadata,
/// not an anonymous access grant.
/// </summary>
/// <remarks>
/// <para>
/// The test seeds, directly against the real isolated SQL Server database, a public-approved
/// product with a produced piece and a public-approved media asset. It then issues anonymous GETs
/// to the catalog and media read endpoints and asserts each returns <c>401</c> (the configured
/// <c>OnRedirectToLogin → 401</c>), crucially including the public-approved product and the
/// public-approved media download. A positive control signs in and reads the very same resources
/// successfully, so the anonymous denial is an authentication gate rather than a missing resource
/// (requirement A5 §7.4; design Property 12).
/// </para>
/// <para>
/// The host runs against real SQL Server built from the actual migrations (not EF InMemory), so the
/// endpoints resolve the seeded rows through the production read path.
/// </para>
/// </remarks>
[Collection(AcceptanceApiTestGroup.Name)]
public sealed class UnauthenticatedPrivateDataAcceptanceTests
{
    private readonly AcceptanceApiFactory _factory;

    public UnauthenticatedPrivateDataAcceptanceTests(AcceptanceApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousReads_OfPublicApprovedResources_AreDeniedWhileAuthenticatedCallerCanReadAsync()
    {
        // Arrange: seed a PUBLIC-APPROVED product with a piece and a PUBLIC-APPROVED media asset
        // directly in the real database. Public approval is exactly the metadata that must NOT open
        // an anonymous door.
        (Guid productId, Guid pieceId, Guid mediaId) = await SeedPublicApprovedGraphAsync();

        // Act + Assert (anonymous): every private read path is refused with 401, even for the
        // public-approved product, piece, and media download.
        using HttpClient anonymous = _factory.CreateApiClient();

        await AssertUnauthorizedAsync(anonymous, $"/api/v1/products/{productId}");
        await AssertUnauthorizedAsync(anonymous, $"/api/v1/products");
        await AssertUnauthorizedAsync(anonymous, $"/api/v1/pieces/{pieceId}");
        await AssertUnauthorizedAsync(anonymous, $"/api/v1/media/{mediaId}");
        await AssertUnauthorizedAsync(anonymous, $"/api/v1/media/{mediaId}/content");

        // Positive control: an authenticated caller CAN read the same product and media metadata, so
        // the resources exist and the anonymous refusal is purely the authentication gate.
        using HttpClient owner = await AcceptanceApiSession.AuthenticateOwnerAsync(_factory);

        using HttpResponseMessage product = await owner.GetAsync($"/api/v1/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, product.StatusCode);

        using HttpResponseMessage media = await owner.GetAsync($"/api/v1/media/{mediaId}");
        Assert.Equal(HttpStatusCode.OK, media.StatusCode);
    }

    private static async Task AssertUnauthorizedAsync(HttpClient client, string requestUri)
    {
        using HttpResponseMessage response = await client.GetAsync(requestUri);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(Guid ProductId, Guid PieceId, Guid MediaId)> SeedPublicApprovedGraphAsync()
    {
        await using PenguinPlankDbContext context = _factory.CreateContext();

        Product product = new()
        {
            Id = Guid.NewGuid(),
            Name = "Published serving board",
            PublicDescription = "Visible to the public catalog.",
            PublicationState = PublicationState.PublicApproved,
            ActiveFlag = true,
        };

        ProductVariant variant = new()
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Sku = $"SKU-ACC11-{Guid.NewGuid():N}",
            TrackingMode = TrackingMode.Serialized,
            UnitOfMeasure = "each",
            ActiveFlag = true,
        };

        ProductPiece piece = new()
        {
            Id = Guid.NewGuid(),
            VariantId = variant.Id,
            PieceCode = $"PC-ACC11-{Guid.NewGuid():N}",
            PublicationState = PublicationState.PublicApproved,
            ActiveFlag = true,
        };

        MediaAsset media = new()
        {
            Id = Guid.NewGuid(),
            StorageKey = $"key-{Guid.NewGuid():N}",
            MimeType = "image/png",
            SizeBytes = 1024,
            Checksum = "deadbeef",
            Caption = "Hero shot",
            Role = "Primary",
            SortOrder = 0,
            Visibility = MediaVisibility.PublicApproved,
        };

        context.Products.Add(product);
        context.ProductVariants.Add(variant);
        context.ProductPieces.Add(piece);
        context.MediaAssets.Add(media);
        await context.SaveChangesAsync();

        return (product.Id, piece.Id, media.Id);
    }
}
