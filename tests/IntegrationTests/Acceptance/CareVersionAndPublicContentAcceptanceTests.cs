using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.UseCases;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Media;
using PenguinPlank.Infrastructure.Auditing;
using PenguinPlank.Infrastructure.Catalog.Persistence;
using PenguinPlank.Infrastructure.Persistence;

namespace IntegrationTests.Acceptance;

/// <summary>
/// Acceptance scenario #12 (requirement R10 §2.4, §2.11): a produced piece preserves the exact
/// care-profile version in effect at production time even after a later care edit, and public-facing
/// content excludes internal notes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Care-version preservation.</b> Driven through the real API: an Owner appends care version 1,
/// creates a piece under a variant that references the care profile (so the piece records the
/// version in effect — v1), then appends care version 2. The test then resolves the piece's
/// preserved version through the real persistence boundary
/// (<see cref="CareProfileStore.ResolveVersionForPieceAsync"/>) over the SQL Server database and
/// asserts it is still version 1, proving a later edit never rewrites an earlier piece's care
/// history (requirement 2.4; design invariant 10). The care profile and the variant referencing it
/// are seeded directly because Phase A exposes no care-profile/variant-care-link creation endpoint;
/// the version append and the piece creation go through the genuine HTTP use cases.
/// </para>
/// <para>
/// <b>Public content excludes internal notes.</b> An Owner creates a product carrying internal
/// notes and reads it back through <c>GET /products/{id}</c>, where the authorized internal view
/// includes those notes. The same product is then run through the public projection surface
/// (<see cref="PublicProjectionPolicy.ToPublicView"/>) over the real SQL-persisted row, and the
/// resulting public view is asserted to exclude the internal notes entirely — structurally (the
/// public shape declares no internal member) and by value (the secret text appears nowhere in its
/// serialized form), so public content never leaks an internal field (requirement 2.11; design
/// Property 9).
/// </para>
/// <para>
/// The host and the persistence assertions all run against a real isolated SQL Server database
/// built from the actual migrations, never EF InMemory (coding-standards §7).
/// </para>
/// </remarks>
[Collection(AcceptanceApiTestGroup.Name)]
public sealed class CareVersionAndPublicContentAcceptanceTests
{
    private const string InternalSecretNote = "SECRET-INTERNAL-margin-is-62-percent";

    private readonly AcceptanceApiFactory _factory;

    public CareVersionAndPublicContentAcceptanceTests(AcceptanceApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Piece_AfterLaterCareEdit_StillResolvesProductionTimeCareVersionAsync()
    {
        // Arrange: seed a care profile and a serialized variant that references it (no API exists to
        // create these in Phase A); everything else goes through the real HTTP use cases.
        using HttpClient owner = await AcceptanceApiSession.AuthenticateOwnerAsync(_factory);
        (Guid careProfileId, Guid variantId) = await SeedCareProfileAndVariantAsync();

        // Append care version 1 through the API.
        Guid versionOneId = await AddCareVersionAsync(owner, careProfileId, "Hand wash and re-oil monthly.");

        // Produce a piece through the API — it records the version in effect now (v1).
        Guid pieceId = await CreatePieceAsync(owner, variantId);

        // A LATER care edit appends version 2 through the API (append-only; v1 is untouched).
        Guid versionTwoId = await AddCareVersionAsync(owner, careProfileId, "Re-oil quarterly.");
        Assert.NotEqual(versionOneId, versionTwoId);

        // Act + Assert: resolve the piece's preserved care version through the REAL persistence
        // boundary over SQL Server. It must still be version 1, despite v2 now existing.
        await using PenguinPlankDbContext context = _factory.CreateContext();
        CareProfileStore store = CreateCareProfileStore(context);

        CareProfileVersionView? resolved =
            await store.ResolveVersionForPieceAsync(pieceId, CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(versionOneId, resolved!.CareProfileVersionId);
        Assert.Equal(1, resolved.VersionNumber);
        Assert.Equal("Hand wash and re-oil monthly.", resolved.Guidance);
    }

    [Fact]
    public async Task PublicContent_ExcludesInternalNotes_WhileOwnerInternalViewIncludesThemAsync()
    {
        // Arrange: an Owner creates a product carrying internal notes through the real API.
        using HttpClient owner = await AcceptanceApiSession.AuthenticateOwnerAsync(_factory);
        Guid productId = await CreateProductWithInternalNotesAsync(owner);

        // The authorized internal view (Owner GET) includes the internal notes.
        JsonElement ownerView = await GetJsonAsync(owner, $"/api/v1/products/{productId}");
        Assert.Equal(InternalSecretNote, ownerView.GetProperty("internalNotes").GetString());

        // Make the product public-approved in the real database (Phase A exposes no publish
        // endpoint on the catalog surface), then load the real row.
        await ApproveForPublicAsync(productId);

        Product stored;
        await using (PenguinPlankDbContext context = _factory.CreateContext())
        {
            stored = await context.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        }

        // Act: project the real product through the public projection surface.
        PublicProductView? publicView = PublicProjectionPolicy.ToPublicView(
            stored,
            System.Array.Empty<MediaAsset>());

        // Assert: a public view is produced for the public-approved product, it exposes the public
        // description, and it excludes the internal notes entirely — by value in its serialized form
        // (the public shape has no internal member at all, which is the structural guarantee).
        Assert.NotNull(publicView);
        Assert.Equal("A beautiful hand-made board.", publicView!.PublicDescription);

        string serializedPublicView = JsonSerializer.Serialize(publicView);
        Assert.DoesNotContain(InternalSecretNote, serializedPublicView, StringComparison.Ordinal);
        Assert.DoesNotContain("internalNotes", serializedPublicView, StringComparison.OrdinalIgnoreCase);
    }

    // =====================================================================================
    // Real persistence wiring — mirrors the catalog-persistence integration tests: build the
    // CareProfileStore over the scoped context and the real append-version use case.
    // =====================================================================================

    private static CareProfileStore CreateCareProfileStore(PenguinPlankDbContext context)
    {
        var addCareVersionUseCase = new AddCareVersionUseCase(
            new EfCareProfileVersionStore(context),
            new GuidIdentifierGenerator(),
            new EfAuditSink(context),
            TimeProvider.System);

        return new CareProfileStore(context, addCareVersionUseCase);
    }

    // =====================================================================================
    // Seed helpers — minimal, explicit (coding-standards §7).
    // =====================================================================================

    private async Task<(Guid CareProfileId, Guid VariantId)> SeedCareProfileAndVariantAsync()
    {
        await using PenguinPlankDbContext context = _factory.CreateContext();

        CareProfile careProfile = new()
        {
            Id = Guid.NewGuid(),
            Name = $"Care {Guid.NewGuid():N}",
            ActiveFlag = true,
        };

        Product product = new()
        {
            Id = Guid.NewGuid(),
            Name = "Serialized board",
            PublicationState = PublicationState.Draft,
            ActiveFlag = true,
        };

        ProductVariant variant = new()
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Sku = $"SKU-ACC12-{Guid.NewGuid():N}",
            TrackingMode = TrackingMode.Serialized,
            UnitOfMeasure = "each",
            CareProfileId = careProfile.Id,
            ActiveFlag = true,
        };

        context.CareProfiles.Add(careProfile);
        context.Products.Add(product);
        context.ProductVariants.Add(variant);
        await context.SaveChangesAsync();

        return (careProfile.Id, variant.Id);
    }

    private async Task ApproveForPublicAsync(Guid productId)
    {
        await using PenguinPlankDbContext context = _factory.CreateContext();
        Product product = await context.Products.SingleAsync(p => p.Id == productId);
        product.PublicationState = PublicationState.PublicApproved;
        await context.SaveChangesAsync();
    }

    // =====================================================================================
    // API drivers.
    // =====================================================================================

    private static async Task<Guid> AddCareVersionAsync(HttpClient owner, Guid careProfileId, string guidance)
    {
        using HttpRequestMessage request =
            new(HttpMethod.Post, $"/api/v1/care-profiles/{careProfileId}/versions")
            {
                Content = JsonContent.Create(new { guidance }),
            };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using HttpResponseMessage response = await owner.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        JsonElement body = await ReadJsonAsync(response);
        return body.GetProperty("careProfileVersionId").GetGuid();
    }

    private static async Task<Guid> CreatePieceAsync(HttpClient owner, Guid variantId)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/pieces")
        {
            Content = JsonContent.Create(new
            {
                variantId,
                pieceCode = $"PC-ACC12-{Guid.NewGuid():N}",
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using HttpResponseMessage response = await owner.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        JsonElement body = await ReadJsonAsync(response);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateProductWithInternalNotesAsync(HttpClient owner)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/products")
        {
            Content = JsonContent.Create(new
            {
                name = "Hand-made board",
                publicDescription = "A beautiful hand-made board.",
                internalNotes = InternalSecretNote,
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using HttpResponseMessage response = await owner.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        JsonElement body = await ReadJsonAsync(response);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string requestUri)
    {
        using HttpResponseMessage response = await client.GetAsync(requestUri);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
