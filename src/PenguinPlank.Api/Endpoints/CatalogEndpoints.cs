using Microsoft.Net.Http.Headers;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Catalog;
using PenguinPlank.Application.Catalog.Projection;
using PenguinPlank.Application.Common;
using PenguinPlank.Application.Idempotency;
using PenguinPlank.Application.IdentityAdministration.Authorization;
using PenguinPlank.Contracts.Catalog;
using PenguinPlank.Contracts.Common;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Maps the thin <c>/api/v1</c> catalog endpoints onto their Application use cases: products,
/// variants, pieces, wood species, and care profiles with their versions (requirements R01 §1.1,
/// §1.6, §1.9; A4 §6.1, §6.2, §6.5; A8 §10.3; A1 §4.3).
/// </summary>
/// <remarks>
/// <para>
/// Every endpoint here is deliberately thin (coding-standards §3): it resolves the authenticated
/// actor at the boundary, maps the request DTO to an Application request, invokes the coarse
/// catalog boundary (<see cref="ICatalogReader"/>, <see cref="ICatalogWriter"/>,
/// <see cref="ICareProfileStore"/>), and maps the typed <see cref="Result"/> / read model to an
/// HTTP response. No business policy lives in this layer. Reads expose the aggregate's opaque
/// concurrency token as the HTTP <c>ETag</c> header; edits require <c>If-Match</c> and pass it as
/// the expected version so a stale edit returns <c>412</c> rather than silently overwriting
/// (requirements 6.5, 6.6). Mutations require an <c>Idempotency-Key</c> and run through the
/// <see cref="IdempotencyPipeline"/> so a replay repeats no effect and a reused key with a
/// changed payload is a <c>409</c> conflict (requirements 6.2–6.4).
/// </para>
/// <para>
/// All endpoints run under the <see cref="AuthorizationPolicyNames.StaffOrOwner"/> policy, so an
/// unauthenticated caller is challenged before a handler runs (requirement A5 §7.4) — a public or
/// public-approved catalog record is never anonymously fetchable through any read path here. The
/// field-level Owner/Staff projection that keeps a Staff response structurally free of Owner-only
/// financial fields is applied server-side: the <c>/variants</c> reads route the read model through
/// <see cref="CatalogVariantResponseProjector"/> with the authenticated actor's role before the
/// response DTO is mapped (requirement A2 §5.10, §5.11). The product, piece, and care-profile
/// responses carry no Owner-only field (only public/internal content and <em>sale</em> prices, both
/// handled elsewhere), so they need no financial projection; the public-vs-internal content strip
/// for anonymous surfaces is the separate <c>PublicProjectionPolicy</c> (Property 9).
/// </para>
/// </remarks>
public static class CatalogEndpoints
{
    /// <summary>The route prefix all Phase A business endpoints share.</summary>
    public const string RoutePrefix = "/api/v1";

    /// <summary>
    /// Maps the catalog endpoints under <see cref="RoutePrefix"/> onto the route builder.
    /// </summary>
    /// <param name="routes">The endpoint route builder to map onto.</param>
    /// <returns>The same <paramref name="routes"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="routes"/> is <see langword="null"/>.</exception>
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        System.ArgumentNullException.ThrowIfNull(routes);

        RouteGroupBuilder group = routes
            .MapGroup(RoutePrefix)
            .RequireAuthorization(AuthorizationPolicyNames.StaffOrOwner);

        MapProductEndpoints(group);
        MapVariantEndpoints(group);
        MapPieceEndpoints(group);
        MapWoodSpeciesEndpoints(group);
        MapCareProfileEndpoints(group);

        return routes;
    }

    private static void MapProductEndpoints(RouteGroupBuilder group)
    {
        RouteGroupBuilder products = group.MapGroup("/products");

        products.MapGet("/", async (
            HttpContext httpContext,
            ICatalogReader reader,
            string? search,
            string? category,
            string? publicationState,
            bool includeArchived,
            int? page,
            int? pageSize,
            string? sort,
            bool desc) =>
        {
            try
            {
                PageRequest pageRequest = CatalogQueryBinder.BuildPageRequest(page, pageSize, sort, desc);
                ProductQuery query = CatalogQueryBinder.BuildProductQuery(
                    search, category, publicationState, includeArchived, pageRequest);

                Page<ProductView> result = await reader
                    .ListProductsAsync(query, httpContext.RequestAborted)
                    .ConfigureAwait(false);

                PagedResponse<ProductResponse> response =
                    CatalogContractMapper.ToPagedResponse(result, CatalogContractMapper.ToResponse);
                return Microsoft.AspNetCore.Http.Results.Ok(response);
            }
            catch (CatalogContractFormatException exception)
            {
                return MalformedRequest(httpContext, exception);
            }
        });

        products.MapGet("/{id:guid}", async (HttpContext httpContext, ICatalogReader reader, Guid id) =>
        {
            ProductView? view = await reader.GetProductAsync(id, httpContext.RequestAborted).ConfigureAwait(false);
            if (view is null)
            {
                return CatalogResults.NotFound(httpContext, "No product matches the supplied id.");
            }

            SetETag(httpContext, view.ETagToken);
            return Microsoft.AspNetCore.Http.Results.Ok(CatalogContractMapper.ToResponse(view));
        });

        products.MapPost("/", async (HttpContext httpContext, ICatalogWriter writer, IdempotencyPipeline idempotency, CreateProductContract body) =>
        {
            if (body is null)
            {
                return MalformedRequest(httpContext, new CatalogContractFormatException("A request body is required."));
            }

            return await RunIdempotentCreateAsync(
                httpContext,
                idempotency,
                operation: "CreateProduct",
                payload: body,
                command: token => writer.CreateProductAsync(CatalogContractMapper.ToRequest(body), ResolveActor(httpContext), token),
                locationFor: id => $"{RoutePrefix}/products/{id}").ConfigureAwait(false);
        });

        products.MapPatch("/{id:guid}", async (HttpContext httpContext, ICatalogWriter writer, IdempotencyPipeline idempotency, Guid id, UpdateProductContract body) =>
        {
            if (body is null)
            {
                return MalformedRequest(httpContext, new CatalogContractFormatException("A request body is required."));
            }

            if (!MutationHeaders.TryGetIfMatch(httpContext, out string? expectedVersion))
            {
                return MissingIfMatch(httpContext);
            }

            return await RunIdempotentUpdateAsync(
                httpContext,
                idempotency,
                operation: "UpdateProduct",
                targetId: id,
                payload: new { id, expectedVersion, body },
                command: token => writer.UpdateProductAsync(
                    CatalogContractMapper.ToRequest(body, id, expectedVersion!), ResolveActor(httpContext), token)).ConfigureAwait(false);
        });
    }

    private static void MapVariantEndpoints(RouteGroupBuilder group)
    {
        RouteGroupBuilder variants = group.MapGroup("/variants");

        variants.MapGet("/", async (
            HttpContext httpContext,
            ICatalogReader reader,
            IRoleProjector<ProductVariantView, CatalogVariantResponseView> projector,
            Guid? productId,
            string? search,
            string? trackingMode,
            string? publicationState,
            bool includeArchived,
            int? page,
            int? pageSize,
            string? sort,
            bool desc) =>
        {
            try
            {
                PageRequest pageRequest = CatalogQueryBinder.BuildPageRequest(page, pageSize, sort, desc);
                CatalogQuery query = CatalogQueryBinder.BuildVariantQuery(
                    productId, search, trackingMode, publicationState, includeArchived, pageRequest);

                Page<ProductVariantView> result = await reader
                    .ListVariantsAsync(query, httpContext.RequestAborted)
                    .ConfigureAwait(false);

                // Route every variant through the role projector with the authenticated actor's
                // role so a Staff response is built from the role-safe base shape and can never
                // carry an Owner-only financial field (requirement A2 §5.10, §5.11).
                Role role = ResolveActor(httpContext).Role;
                PagedResponse<VariantResponse> response = CatalogContractMapper.ToPagedResponse(
                    result,
                    view => CatalogContractMapper.ToResponse(projector.Project(view, role)));
                return Microsoft.AspNetCore.Http.Results.Ok(response);
            }
            catch (CatalogContractFormatException exception)
            {
                return MalformedRequest(httpContext, exception);
            }
        });

        variants.MapGet("/{id:guid}", async (
            HttpContext httpContext,
            ICatalogReader reader,
            IRoleProjector<ProductVariantView, CatalogVariantResponseView> projector,
            Guid id) =>
        {
            ProductVariantView? view = await reader.GetVariantAsync(id, httpContext.RequestAborted).ConfigureAwait(false);
            if (view is null)
            {
                return CatalogResults.NotFound(httpContext, "No variant matches the supplied id.");
            }

            SetETag(httpContext, view.ETagToken);

            // Project for the authenticated actor's role before mapping to the transport DTO, so
            // the field-level allowlist is applied server-side (requirement A2 §5.10, §5.11).
            Role role = ResolveActor(httpContext).Role;
            CatalogVariantResponseView projected = projector.Project(view, role);
            return Microsoft.AspNetCore.Http.Results.Ok(CatalogContractMapper.ToResponse(projected));
        });

        variants.MapPost("/", async (HttpContext httpContext, ICatalogWriter writer, IdempotencyPipeline idempotency, CreateVariantContract body) =>
        {
            if (body is null)
            {
                return MalformedRequest(httpContext, new CatalogContractFormatException("A request body is required."));
            }

            CreateVariantRequest request;
            try
            {
                request = CatalogContractMapper.ToRequest(body);
            }
            catch (CatalogContractFormatException exception)
            {
                return MalformedRequest(httpContext, exception);
            }

            return await RunIdempotentCreateAsync(
                httpContext,
                idempotency,
                operation: "CreateVariant",
                payload: body,
                command: token => writer.CreateVariantAsync(request, ResolveActor(httpContext), token),
                locationFor: id => $"{RoutePrefix}/variants/{id}").ConfigureAwait(false);
        });

        variants.MapPatch("/{id:guid}", async (HttpContext httpContext, ICatalogWriter writer, IdempotencyPipeline idempotency, Guid id, UpdateVariantContract body) =>
        {
            if (body is null)
            {
                return MalformedRequest(httpContext, new CatalogContractFormatException("A request body is required."));
            }

            if (!MutationHeaders.TryGetIfMatch(httpContext, out string? expectedVersion))
            {
                return MissingIfMatch(httpContext);
            }

            return await RunIdempotentUpdateAsync(
                httpContext,
                idempotency,
                operation: "UpdateVariant",
                targetId: id,
                payload: new { id, expectedVersion, body },
                command: token => writer.UpdateVariantAsync(
                    CatalogContractMapper.ToRequest(body, id, expectedVersion!), ResolveActor(httpContext), token)).ConfigureAwait(false);
        });
    }

    private static void MapPieceEndpoints(RouteGroupBuilder group)
    {
        RouteGroupBuilder pieces = group.MapGroup("/pieces");

        pieces.MapGet("/", async (
            HttpContext httpContext,
            ICatalogReader reader,
            Guid? variantId,
            string? search,
            string? publicationState,
            bool includeArchived,
            int? page,
            int? pageSize,
            string? sort,
            bool desc) =>
        {
            try
            {
                PageRequest pageRequest = CatalogQueryBinder.BuildPageRequest(page, pageSize, sort, desc);
                PieceQuery query = CatalogQueryBinder.BuildPieceQuery(
                    variantId, search, publicationState, includeArchived, pageRequest);

                Page<ProductPieceView> result = await reader
                    .ListPiecesAsync(query, httpContext.RequestAborted)
                    .ConfigureAwait(false);

                PagedResponse<PieceResponse> response =
                    CatalogContractMapper.ToPagedResponse(result, CatalogContractMapper.ToResponse);
                return Microsoft.AspNetCore.Http.Results.Ok(response);
            }
            catch (CatalogContractFormatException exception)
            {
                return MalformedRequest(httpContext, exception);
            }
        });

        pieces.MapGet("/{id:guid}", async (HttpContext httpContext, ICatalogReader reader, Guid id) =>
        {
            ProductPieceView? view = await reader.GetPieceAsync(id, httpContext.RequestAborted).ConfigureAwait(false);
            if (view is null)
            {
                return CatalogResults.NotFound(httpContext, "No piece matches the supplied id.");
            }

            SetETag(httpContext, view.ETagToken);
            return Microsoft.AspNetCore.Http.Results.Ok(CatalogContractMapper.ToResponse(view));
        });

        pieces.MapPost("/", async (HttpContext httpContext, ICatalogWriter writer, IdempotencyPipeline idempotency, CreatePieceContract body) =>
        {
            if (body is null)
            {
                return MalformedRequest(httpContext, new CatalogContractFormatException("A request body is required."));
            }

            return await RunIdempotentCreateAsync(
                httpContext,
                idempotency,
                operation: "CreatePiece",
                payload: body,
                command: token => writer.CreatePieceAsync(CatalogContractMapper.ToRequest(body), ResolveActor(httpContext), token),
                locationFor: id => $"{RoutePrefix}/pieces/{id}").ConfigureAwait(false);
        });
    }

    private static void MapWoodSpeciesEndpoints(RouteGroupBuilder group)
    {
        // Wood species are referenced by a variant's/piece's wood composition
        // (WoodComponent.WoodSpeciesId). Phase A's Application surface exposes no standalone
        // wood-species read/write use case — the catalog reader/writer and care-profile store are
        // the only catalog boundaries task 8 defined. Rather than put business logic in the API
        // layer (coding-standards §3), the wood-species route group is reserved here and returns a
        // clear 404 until a later task adds the Application operations that back it. No endpoint
        // invents wood-species CRUD at the boundary.
        group.MapGet("/wood-species", (HttpContext httpContext) =>
            CatalogResults.NotFound(
                httpContext,
                "Wood species are managed through a variant's or piece's wood composition in Phase A; no standalone wood-species resource is exposed yet."));
    }

    private static void MapCareProfileEndpoints(RouteGroupBuilder group)
    {
        RouteGroupBuilder careProfiles = group.MapGroup("/care-profiles");

        careProfiles.MapGet("/{id:guid}/versions", async (HttpContext httpContext, ICareProfileStore store, Guid id) =>
        {
            IReadOnlyList<CareProfileVersionView> versions = await store
                .GetVersionsAsync(id, httpContext.RequestAborted)
                .ConfigureAwait(false);

            IReadOnlyList<CareProfileVersionResponse> response =
                [.. versions.Select(CatalogContractMapper.ToResponse)];
            return Microsoft.AspNetCore.Http.Results.Ok(response);
        });

        careProfiles.MapPost("/{id:guid}/versions", async (HttpContext httpContext, ICareProfileStore store, IdempotencyPipeline idempotency, Guid id, AddCareVersionContract body) =>
        {
            if (body is null || string.IsNullOrWhiteSpace(body.Guidance))
            {
                return MalformedRequest(httpContext, new CatalogContractFormatException("A care version requires non-empty guidance."));
            }

            if (!MutationHeaders.TryGetIdempotencyKey(httpContext, out string? key))
            {
                return MissingIdempotencyKey(httpContext);
            }

            ActorContext actor = ResolveActor(httpContext);
            var request = new AddCareVersionRequest(id, body.Guidance);

            IdempotencyPipelineResult<Result<CareProfileVersionView>> outcome = await idempotency
                .ExecuteAsync(
                    new IdempotencyKey(key!),
                    Caller.FromActor(actor),
                    new Operation("AddCareVersion"),
                    PayloadHasher.Compute(CanonicalPayload(new { id, body.Guidance })),
                    async token =>
                    {
                        Result<CareProfileVersionView> result = await store
                            .AddVersionAsync(request, actor, token)
                            .ConfigureAwait(false);
                        string reference = result.IsSuccess ? result.Value.CareProfileVersionId.ToString() : "rejected";
                        return new CommandExecution<Result<CareProfileVersionView>>(result, new ResultRef(reference));
                    },
                    httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (outcome.WasConflict)
            {
                return CatalogResults.Problem(outcome.Conflict!.Value, httpContext);
            }

            if (outcome.WasReplayed)
            {
                return Microsoft.AspNetCore.Http.Results.Ok();
            }

            Result<CareProfileVersionView> executed = outcome.Value;
            if (executed.IsFailure)
            {
                return CatalogResults.Problem(executed.Error, httpContext);
            }

            CareProfileVersionResponse created = CatalogContractMapper.ToResponse(executed.Value);
            return Microsoft.AspNetCore.Http.Results.Created(
                $"{RoutePrefix}/care-profiles/{id}/versions/{created.CareProfileVersionId}",
                created);
        });
    }

    private static ActorContext ResolveActor(HttpContext httpContext) =>
        ActorContextResolver.Resolve(httpContext.User);

    private static void SetETag(HttpContext httpContext, string eTagToken) =>
        httpContext.Response.Headers[HeaderNames.ETag] = MutationHeaders.ToETagHeader(eTagToken);

    private static async Task<IResult> RunIdempotentCreateAsync(
        HttpContext httpContext,
        IdempotencyPipeline idempotency,
        string operation,
        object payload,
        System.Func<CancellationToken, Task<Result<Guid>>> command,
        System.Func<Guid, string> locationFor)
    {
        if (!MutationHeaders.TryGetIdempotencyKey(httpContext, out string? key))
        {
            return MissingIdempotencyKey(httpContext);
        }

        ActorContext actor = ResolveActor(httpContext);

        IdempotencyPipelineResult<Result<Guid>> outcome = await idempotency
            .ExecuteAsync(
                new IdempotencyKey(key!),
                Caller.FromActor(actor),
                new Operation(operation),
                PayloadHasher.Compute(CanonicalPayload(payload)),
                async token =>
                {
                    Result<Guid> result = await command(token).ConfigureAwait(false);
                    string reference = result.IsSuccess ? result.Value.ToString() : "rejected";
                    return new CommandExecution<Result<Guid>>(result, new ResultRef(reference));
                },
                httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (outcome.WasConflict)
        {
            return CatalogResults.Problem(outcome.Conflict!.Value, httpContext);
        }

        if (outcome.WasReplayed)
        {
            // The original command already produced the effect; replay its stored reference without
            // repeating it (requirement A4 §6.4).
            string? storedId = outcome.StoredResult?.Value;
            if (!string.IsNullOrEmpty(storedId) && Guid.TryParse(storedId, out Guid replayedId))
            {
                return Microsoft.AspNetCore.Http.Results.Created(locationFor(replayedId), new { id = replayedId });
            }

            return Microsoft.AspNetCore.Http.Results.Ok();
        }

        Result<Guid> executed = outcome.Value;
        if (executed.IsFailure)
        {
            return CatalogResults.Problem(executed.Error, httpContext);
        }

        Guid id = executed.Value;
        return Microsoft.AspNetCore.Http.Results.Created(locationFor(id), new { id });
    }

    private static async Task<IResult> RunIdempotentUpdateAsync(
        HttpContext httpContext,
        IdempotencyPipeline idempotency,
        string operation,
        Guid targetId,
        object payload,
        System.Func<CancellationToken, Task<Result>> command)
    {
        if (!MutationHeaders.TryGetIdempotencyKey(httpContext, out string? key))
        {
            return MissingIdempotencyKey(httpContext);
        }

        ActorContext actor = ResolveActor(httpContext);

        IdempotencyPipelineResult<Result> outcome = await idempotency
            .ExecuteAsync(
                new IdempotencyKey(key!),
                Caller.FromActor(actor),
                new Operation(operation),
                PayloadHasher.Compute(CanonicalPayload(payload)),
                async token =>
                {
                    Result result = await command(token).ConfigureAwait(false);
                    return new CommandExecution<Result>(result, new ResultRef(targetId.ToString()));
                },
                httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (outcome.WasConflict)
        {
            return CatalogResults.Problem(outcome.Conflict!.Value, httpContext);
        }

        if (outcome.WasReplayed)
        {
            return Microsoft.AspNetCore.Http.Results.NoContent();
        }

        Result executed = outcome.Value;
        if (executed.IsFailure)
        {
            return CatalogResults.Problem(executed.Error, httpContext);
        }

        return Microsoft.AspNetCore.Http.Results.NoContent();
    }

    private static string CanonicalPayload(object payload) =>
        System.Text.Json.JsonSerializer.Serialize(payload);

    private static IResult MissingIdempotencyKey(HttpContext httpContext) =>
        ValidationProblem(httpContext, $"The '{MutationHeaders.IdempotencyKeyHeader}' header is required on a mutating request.");

    private static IResult MissingIfMatch(HttpContext httpContext) =>
        ValidationProblem(httpContext, "The 'If-Match' header is required to edit a mutable record (optimistic concurrency).");

    private static IResult MalformedRequest(HttpContext httpContext, CatalogContractFormatException exception)
    {
        System.ArgumentNullException.ThrowIfNull(exception);
        return ValidationProblem(httpContext, exception.Message);
    }

    private static IResult ValidationProblem(HttpContext httpContext, string detail) =>
        CatalogResults.Problem(new BusinessError(ErrorCode.Validation, detail), httpContext);
}
