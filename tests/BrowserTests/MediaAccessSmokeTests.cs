using System.Net;
using System.Text.Json;
using BrowserTests.Hosting;
using Microsoft.Playwright;

namespace BrowserTests;

/// <summary>
/// Smoke tests for media access through a real browser session: an authorized actor can upload and
/// download an image, and an anonymous caller is refused every media path (requirements A5 7.5,
/// A4 6.11).
/// </summary>
/// <remarks>
/// Phase A's client has no media screen, so the requests are sent through the browser context's
/// request API. That API shares the context's cookie jar, so the calls carry exactly the session the
/// UI sign-in created (or nothing, for the anonymous context).
/// </remarks>
[Collection(BrowserWorkflowTestGroup.Name)]
public sealed class MediaAccessSmokeTests
{
    private const string PngContentType = "image/png";
    private const string UploadFieldName = "file";

    // A valid 1x1 PNG: it carries the PNG magic-byte signature the upload policy requires.
    private static readonly byte[] s_pngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly BrowserWorkflowFixture _fixture;

    public MediaAccessSmokeTests(BrowserWorkflowFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Media_AuthorizedSession_UploadsAndDownloadsSameBytesAsync()
    {
        // Arrange
        await using IBrowserContext context = await _fixture.NewContextAsync();
        IPage page = await context.NewPageAsync();
        await _fixture.SignInAsync(page, BrowserApiFactory.OwnerEmail, BrowserApiFactory.OwnerPassword);

        // Act: upload, then download what was stored.
        Guid mediaAssetId = await UploadPngAsync(context.APIRequest);
        IAPIResponse download = await context.APIRequest.GetAsync(ContentUrl(mediaAssetId));

        // Assert: the authorized session gets back exactly the stored bytes.
        Assert.Equal((int)HttpStatusCode.OK, download.Status);
        Assert.Equal(s_pngBytes, await download.BodyAsync());
    }

    [Fact]
    public async Task Media_AnonymousSession_IsDeniedUploadMetadataAndDownloadAsync()
    {
        // Arrange: an asset exists that an authorized session uploaded.
        Guid mediaAssetId;
        await using (IBrowserContext authorized = await _fixture.NewContextAsync())
        {
            IPage authorizedPage = await authorized.NewPageAsync();
            await _fixture.SignInAsync(authorizedPage, BrowserApiFactory.OwnerEmail, BrowserApiFactory.OwnerPassword);
            mediaAssetId = await UploadPngAsync(authorized.APIRequest);
        }

        // Act: a brand-new context holds no session cookie.
        await using IBrowserContext anonymous = await _fixture.NewContextAsync();
        IAPIResponse download = await anonymous.APIRequest.GetAsync(ContentUrl(mediaAssetId));
        IAPIResponse metadata = await anonymous.APIRequest.GetAsync($"{_fixture.BaseUrl}/api/v1/media/{mediaAssetId}");
        IAPIResponse upload = await anonymous.APIRequest.PostAsync(
            $"{_fixture.BaseUrl}/api/v1/media",
            new APIRequestContextOptions { Multipart = BuildPngForm(anonymous.APIRequest) });

        // Assert
        Assert.Equal((int)HttpStatusCode.Unauthorized, download.Status);
        Assert.Equal((int)HttpStatusCode.Unauthorized, metadata.Status);
        Assert.Equal((int)HttpStatusCode.Unauthorized, upload.Status);
    }

    private string ContentUrl(Guid mediaAssetId) =>
        $"{_fixture.BaseUrl}/api/v1/media/{mediaAssetId}/content";

    private async Task<Guid> UploadPngAsync(IAPIRequestContext request)
    {
        IAPIResponse response = await request.PostAsync(
            $"{_fixture.BaseUrl}/api/v1/media",
            new APIRequestContextOptions { Multipart = BuildPngForm(request) });

        Assert.Equal((int)HttpStatusCode.Created, response.Status);

        JsonElement? body = await response.JsonAsync();
        Assert.NotNull(body);
        return body.Value.GetProperty("mediaAssetId").GetGuid();
    }

    private static IFormData BuildPngForm(IAPIRequestContext request) =>
        request.CreateFormData().Set(
            UploadFieldName,
            new FilePayload
            {
                Name = "pixel.png",
                MimeType = PngContentType,
                Buffer = s_pngBytes,
            });
}
