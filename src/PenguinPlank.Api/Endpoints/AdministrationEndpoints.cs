using Microsoft.Net.Http.Headers;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Common;
using PenguinPlank.Application.IdentityAdministration;
using PenguinPlank.Application.IdentityAdministration.Administration;
using PenguinPlank.Application.IdentityAdministration.Authorization;
using PenguinPlank.Contracts.Administration;
using PenguinPlank.Contracts.Common;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Maps the thin Owner-only administration endpoints onto their Application use cases:
/// <c>/settings</c>, <c>/users</c>, <c>/audit</c>, and the one-time Owner bootstrap (requirement A2
/// §5.3, §5.8, §5.9, §5.10, §5.12).
/// </summary>
/// <remarks>
/// <para>
/// Every endpoint here is deliberately thin (coding-standards §3): it resolves the authenticated
/// actor at the boundary, maps the versioned Contracts DTO to an Application request, invokes the
/// use case, and maps the typed <see cref="Result"/> / read model to an HTTP response. No business
/// policy lives in this layer. The settings edit requires an <c>If-Match</c> header and passes it
/// as the expected version so a stale edit returns <c>412</c> rather than overwriting newer
/// settings (requirements A4 §6.5, §6.6).
/// </para>
/// <para>
/// <b>Authorization.</b> The whole administration group runs under
/// <see cref="AuthorizationPolicyNames.OwnerOnly"/> — settings, user administration, and the audit
/// trail are all Owner-only; Staff never reaches them (requirement A2 §5.10). The audit trail is
/// treated as sensitive, so it is Owner-only too. The field-level projection seam for responses is
/// task 9.4; this task wires the authenticated actor and the Owner policy.
/// </para>
/// </remarks>
public static class AdministrationEndpoints
{
    /// <summary>The route prefix the administration endpoints share with the other Phase A business endpoints.</summary>
    public const string RoutePrefix = "/api/v1";

    /// <summary>
    /// Maps the administration endpoints under <see cref="RoutePrefix"/> onto the route builder.
    /// </summary>
    /// <param name="routes">The endpoint route builder to map onto.</param>
    /// <returns>The same <paramref name="routes"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="routes"/> is <see langword="null"/>.</exception>
    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder routes)
    {
        System.ArgumentNullException.ThrowIfNull(routes);

        RouteGroupBuilder group = routes
            .MapGroup(RoutePrefix)
            .RequireAuthorization(AuthorizationPolicyNames.OwnerOnly);

        MapSettingsEndpoints(group);
        MapUserEndpoints(group);
        MapAuditEndpoints(group);

        // The one-time Owner bootstrap is mapped OUTSIDE the Owner-only group: on first run no
        // Owner exists yet, so requiring the Owner role would make it unreachable (a chicken-and-egg
        // lock-out). It is mapped separately and allowed anonymously; see MapOwnerBootstrapEndpoint
        // for why that is safe.
        MapOwnerBootstrapEndpoint(routes);

        return routes;
    }

    private static void MapSettingsEndpoints(RouteGroupBuilder group)
    {
        RouteGroupBuilder settings = group.MapGroup("/settings");

        settings.MapGet("/", async (HttpContext httpContext, GetBusinessSettingsUseCase useCase) =>
        {
            BusinessSettingsView? view = await useCase.ExecuteAsync(httpContext.RequestAborted).ConfigureAwait(false);
            if (view is null)
            {
                return CatalogResults.NotFound(httpContext, "The business settings have not been initialized.");
            }

            httpContext.Response.Headers[HeaderNames.ETag] = MutationHeaders.ToETagHeader(view.ETagToken);
            return Microsoft.AspNetCore.Http.Results.Ok(AdministrationContractMapper.ToResponse(view));
        });

        settings.MapPatch("/", async (HttpContext httpContext, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, UpdateBusinessSettingsUseCase useCase, UpdateBusinessSettingsContract body) =>
        {
            IResult? csrfFailure = await AntiforgeryGuard.ValidateAsync(httpContext, antiforgery).ConfigureAwait(false);
            if (csrfFailure is not null)
            {
                return csrfFailure;
            }

            if (body is null)
            {
                return CatalogResults.Problem(new BusinessError(ErrorCode.Validation, "A request body is required."), httpContext);
            }

            if (!MutationHeaders.TryGetIfMatch(httpContext, out string? expectedVersion))
            {
                return CatalogResults.Problem(
                    new BusinessError(ErrorCode.Validation, "The 'If-Match' header is required to edit settings (optimistic concurrency)."),
                    httpContext);
            }

            UpdateBusinessSettingsRequest request = AdministrationContractMapper.ToRequest(body, expectedVersion!);

            Result<BusinessSettingsView> result = await useCase
                .ExecuteAsync(request, ResolveActor(httpContext), httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                return CatalogResults.Problem(result.Error, httpContext);
            }

            httpContext.Response.Headers[HeaderNames.ETag] = MutationHeaders.ToETagHeader(result.Value.ETagToken);
            return Microsoft.AspNetCore.Http.Results.Ok(AdministrationContractMapper.ToResponse(result.Value));
        });
    }

    private static void MapUserEndpoints(RouteGroupBuilder group)
    {
        RouteGroupBuilder users = group.MapGroup("/users");

        users.MapGet("/", async (HttpContext httpContext, ListStaffUsersUseCase useCase) =>
        {
            IReadOnlyList<StaffUserView> staff = await useCase.ExecuteAsync(httpContext.RequestAborted).ConfigureAwait(false);
            IReadOnlyList<StaffUserResponse> response = [.. staff.Select(AdministrationContractMapper.ToResponse)];
            return Microsoft.AspNetCore.Http.Results.Ok(response);
        });

        users.MapPost("/", async (HttpContext httpContext, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, CreateStaffUserUseCase useCase, CreateStaffUserContract body) =>
        {
            IResult? csrfFailure = await AntiforgeryGuard.ValidateAsync(httpContext, antiforgery).ConfigureAwait(false);
            if (csrfFailure is not null)
            {
                return csrfFailure;
            }

            if (body is null)
            {
                return CatalogResults.Problem(new BusinessError(ErrorCode.Validation, "A request body is required."), httpContext);
            }

            CreateStaffUserRequest request;
            try
            {
                request = AdministrationContractMapper.ToRequest(body);
            }
            catch (CatalogContractFormatException exception)
            {
                return CatalogResults.Problem(new BusinessError(ErrorCode.Validation, exception.Message), httpContext);
            }

            Result<StaffUserView> result = await useCase
                .ExecuteAsync(request, ResolveActor(httpContext), httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                return CatalogResults.Problem(result.Error, httpContext);
            }

            StaffUserResponse created = AdministrationContractMapper.ToResponse(result.Value);
            return Microsoft.AspNetCore.Http.Results.Created($"{RoutePrefix}/users/{created.UserId}", created);
        });
    }

    private static void MapAuditEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/audit", async (
            HttpContext httpContext,
            ListAuditEntriesUseCase useCase,
            Guid? actorId,
            string? entityType,
            Guid? entityId,
            int? page,
            int? pageSize) =>
        {
            var pageRequest = new PageRequest(page ?? PageRequest.MinimumPageNumber, pageSize);
            var query = new AuditQuery(pageRequest, actorId, entityType, entityId);

            Page<AuditEntryView> result = await useCase.ExecuteAsync(query, httpContext.RequestAborted).ConfigureAwait(false);

            var response = new PagedResponse<AuditEntryResponse>
            {
                Items = [.. result.Items.Select(AdministrationContractMapper.ToResponse)],
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
            };
            return Microsoft.AspNetCore.Http.Results.Ok(response);
        });
    }

    private static void MapOwnerBootstrapEndpoint(IEndpointRouteBuilder routes)
    {
        // The one-time, idempotent Owner bootstrap (requirement A2 §5.8).
        //
        // It is allowed anonymously because it must run on first deployment, when no Owner (and so
        // no authenticated caller) can possibly exist — an Owner-only gate would be an unopenable
        // lock. This is NOT an anonymous "create any owner" surface:
        //   * The Owner credentials are NOT taken from the request body; they come only from the
        //     operator-supplied OwnerBootstrapOptions bound from deployment configuration, so a
        //     caller cannot choose an account to create.
        //   * The bootstrapper is idempotent: once an Owner exists it makes no change and reports
        //     AlreadyExists, so a repeated or hostile call after setup is a harmless no-op.
        //   * A missing or placeholder configured password is refused rather than defaulted.
        // Together these make the endpoint safe to expose for the first-run operator to invoke.
        //
        // It is also exempted from the antiforgery (CSRF) middleware via DisableAntiforgery(). CSRF
        // protects authenticated, session-cookie, body-driven state changes; none of those apply
        // here. This call is anonymous (no session cookie to abuse), takes NO request-body input
        // (the Owner credentials come only from server-side OwnerBootstrapOptions configuration,
        // never the request), and is idempotent (a no-op once an Owner exists). Antiforgery adds no
        // security value for it and — because UseAntiforgery validates minimal-API endpoints unless
        // they opt out — would otherwise reject the legitimate first-run call before the handler
        // runs. ONLY this endpoint is exempt; the authenticated mutations keep their AntiforgeryGuard.
        routes.MapPost($"{RoutePrefix}/owner-bootstrap", async (HttpContext httpContext, IOwnerBootstrapper bootstrapper, IActorContextAccessor actorContextAccessor) =>
        {
            // The bootstrap is an anonymous first-run action — no interactive principal exists yet —
            // but creating the Owner still writes Identity rows, so the audit SaveChanges interceptor
            // needs an actor for this unit of work. It is a system-initiated mutation, so record the
            // recognizable system actor at this boundary (requirements A2 §5.12, A4 §6.7, A7 §9.6),
            // mirroring how the background worker runs through application services.
            actorContextAccessor.SetCurrent(ActorContext.SystemWorker());

            Result<OwnerBootstrapOutcome> result = await bootstrapper
                .EnsureOwnerAsync(httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                return CatalogResults.Problem(result.Error, httpContext);
            }

            return Microsoft.AspNetCore.Http.Results.Ok(new { outcome = result.Value.ToString() });
        })
        .AllowAnonymous()
        .DisableAntiforgery();
    }

    private static ActorContext ResolveActor(HttpContext httpContext) =>
        ActorContextResolver.Resolve(httpContext.User);
}
