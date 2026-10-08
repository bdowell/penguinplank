using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Media;
using PenguinPlank.Application.Media.UseCases;
using PenguinPlank.Contracts.Media;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Maps the thin <c>/api/v1/media</c> endpoints onto their Application media use cases: validated
/// upload, authorized download, and metadata read/edit (requirement A4 §6.8–6.11).
/// </summary>
/// <remarks>
/// <para>
/// Every endpoint here is deliberately thin (coding-standards §3): it resolves the authenticated
/// actor at the boundary, hands an Application use case ordinary inputs, and maps the typed
/// <see cref="Result"/> / read model to an HTTP response. No upload validation, storage-key
/// assignment, or persistence decision lives in this layer — that is the
/// <see cref="UploadMediaUseCase"/> orchestrating the pure <c>MediaUploadValidationPolicy</c>
/// through <see cref="IFileStore"/> (requirements 6.9, 6.10).
/// </para>
/// <para>
/// All endpoints run under <see cref="PenguinPlank.Application.IdentityAdministration.Authorization.AuthorizationPolicyNames.StaffOrOwner"/>,
/// so an unauthenticated caller is challenged before any handler runs. This is the enforcement
/// point for requirement 6.11: a download requires an authenticated authorized actor, and a
/// public-approved asset is therefore <b>never</b> anonymously downloadable — public-approved
/// visibility is metadata/eligibility only and is not consulted as an access grant. The
/// field-level Owner/Staff projection of metadata responses and the formal deny-unauthenticated
/// property/integration tests are tasks 9.4/9.5/9.6; this task wires the authenticated actor and
/// the role policy and ensures anonymous download is refused.
/// </para>
/// </remarks>
public static class MediaEndpoints
{
    /// <summary>The route prefix the media endpoints share with the other Phase A business endpoints.</summary>
    public const string RoutePrefix = "/api/v1";

    /// <summary>
    /// Maps the media endpoints under <see cref="RoutePrefix"/> onto the route builder.
    /// </summary>
    /// <param name="routes">The endpoint route builder to map onto.</param>
    /// <returns>The same <paramref name="routes"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="routes"/> is <see langword="null"/>.</exception>
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder routes)
    {
        System.ArgumentNullException.ThrowIfNull(routes);

        RouteGroupBuilder media = routes
            .MapGroup($"{RoutePrefix}/media")
            .RequireAuthorization(
                PenguinPlank.Application.IdentityAdministration.Authorization.AuthorizationPolicyNames.StaffOrOwner);

        MapUpload(media);
        MapDownload(media);
        MapGetMetadata(media);
        MapUpdateMetadata(media);

        return routes;
    }

    private static void MapUpload(RouteGroupBuilder media)
    {
        media.MapPost("/", async (HttpContext httpContext, UploadMediaUseCase useCase, IFormFile? file) =>
        {
            if (file is null || file.Length <= 0)
            {
                return CatalogResults.Problem(
                    new BusinessError(ErrorCode.Validation, "A non-empty file is required on a media upload."),
                    httpContext);
            }

            ActorContext actor = ResolveActor(httpContext);

            await using Stream content = file.OpenReadStream();
            var upload = new MediaUpload(
                file.ContentType ?? string.Empty,
                file.Length,
                content,
                file.FileName);

            Result<MediaAssetView> result = await useCase
                .ExecuteAsync(upload, actor, httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                return CatalogResults.Problem(result.Error, httpContext);
            }

            MediaAssetResponse response = MediaContractMapper.ToResponse(result.Value);
            return Microsoft.AspNetCore.Http.Results.Created(
                $"{RoutePrefix}/media/{response.MediaAssetId}",
                response);
        })
        .DisableAntiforgery();
    }

    private static void MapDownload(RouteGroupBuilder media)
    {
        media.MapGet("/{id:guid}/content", async (HttpContext httpContext, DownloadMediaUseCase useCase, Guid id) =>
        {
            ActorContext actor = ResolveActor(httpContext);

            Result<MediaContent> result = await useCase
                .ExecuteAsync(id, actor, httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                return result.Error.Code == ErrorCode.Validation
                    ? CatalogResults.NotFound(httpContext, "No media asset matches the supplied id.")
                    : CatalogResults.Problem(result.Error, httpContext);
            }

            MediaContent downloaded = result.Value;
            return Microsoft.AspNetCore.Http.Results.Stream(
                downloaded.Stream,
                downloaded.ContentType);
        });
    }

    private static void MapGetMetadata(RouteGroupBuilder media)
    {
        media.MapGet("/{id:guid}", async (HttpContext httpContext, GetMediaMetadataUseCase useCase, Guid id) =>
        {
            MediaAssetView? view = await useCase.ExecuteAsync(id, httpContext.RequestAborted).ConfigureAwait(false);
            if (view is null)
            {
                return CatalogResults.NotFound(httpContext, "No media asset matches the supplied id.");
            }

            return Microsoft.AspNetCore.Http.Results.Ok(MediaContractMapper.ToResponse(view));
        });
    }

    private static void MapUpdateMetadata(RouteGroupBuilder media)
    {
        media.MapPatch("/{id:guid}", async (HttpContext httpContext, UpdateMediaMetadataUseCase useCase, Guid id, UpdateMediaMetadataContract body) =>
        {
            if (body is null)
            {
                return CatalogResults.Problem(
                    new BusinessError(ErrorCode.Validation, "A request body is required."),
                    httpContext);
            }

            UpdateMediaMetadataRequest request;
            try
            {
                request = MediaContractMapper.ToRequest(body, id);
            }
            catch (CatalogContractFormatException exception)
            {
                return CatalogResults.Problem(new BusinessError(ErrorCode.Validation, exception.Message), httpContext);
            }

            Result<MediaAssetView> result = await useCase
                .ExecuteAsync(request, ResolveActor(httpContext), httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                return result.Error.Code == ErrorCode.Validation
                    ? CatalogResults.NotFound(httpContext, "No media asset matches the supplied id.")
                    : CatalogResults.Problem(result.Error, httpContext);
            }

            return Microsoft.AspNetCore.Http.Results.Ok(MediaContractMapper.ToResponse(result.Value));
        });
    }

    private static ActorContext ResolveActor(HttpContext httpContext) =>
        ActorContextResolver.Resolve(httpContext.User);
}
