using Microsoft.AspNetCore.Antiforgery;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Idempotency;
using PenguinPlank.Application.IdentityAdministration.Authorization;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Contracts.Integrations;
using PenguinPlank.Domain.Common;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Maps the thin Owner-only integration management endpoints under
/// <c>/api/v1/integrations</c>: connections, mappings, sync runs, exceptions, and the pause,
/// resume, and retry actions (requirements R11 / 3.12, 3.13; A7 / 9.5).
/// </summary>
/// <remarks>
/// <para>
/// Every endpoint is thin (coding-standards §3): it resolves the authenticated actor at the
/// boundary, maps Contracts DTOs to Application requests, calls an existing Phase A store or
/// service, and maps the typed outcome to HTTP. No integration business rule lives here.
/// </para>
/// <para>
/// <b>Authorization.</b> The whole group runs under <see cref="AuthorizationPolicyNames.OwnerOnly"/>,
/// so Staff receive <c>403</c> for connection, credential-state, mapping, and sync controls
/// (requirement A7 / 9.5). No response carries a credential reference or secret: the connection
/// read model has no such field, and exception errors are already redacted when queued.
/// </para>
/// <para>
/// <b>Mutations</b> require an antiforgery token and an <c>Idempotency-Key</c> and run through
/// <see cref="IdempotencyPipeline"/>, so a replay repeats no effect. Connectors are disabled in
/// Phase A and the stores offer no way to enable one, so nothing here dispatches to a provider. No
/// endpoint here edits a versioned aggregate in place, so there is no <c>If-Match</c> precondition
/// to enforce; pause, resume, and retry are idempotent state transitions.
/// </para>
/// </remarks>
public static class IntegrationEndpoints
{
    /// <summary>The route prefix shared with the other Phase A business endpoints.</summary>
    public const string RoutePrefix = "/api/v1/integrations";

    /// <summary>Maps the integration management endpoints onto the route builder.</summary>
    /// <param name="routes">The endpoint route builder to map onto.</param>
    /// <returns>The same <paramref name="routes"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="routes"/> is <see langword="null"/>.</exception>
    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder routes)
    {
        System.ArgumentNullException.ThrowIfNull(routes);

        RouteGroupBuilder group = routes
            .MapGroup(RoutePrefix)
            .RequireAuthorization(AuthorizationPolicyNames.OwnerOnly);

        MapConnectionEndpoints(group);
        MapMappingEndpoints(group);
        MapSyncRunEndpoints(group);
        MapExceptionEndpoints(group);

        return routes;
    }

    private static void MapConnectionEndpoints(RouteGroupBuilder group)
    {
        RouteGroupBuilder connections = group.MapGroup("/connections");

        connections.MapGet("/", async (HttpContext httpContext, IIntegrationConnectionStore store) =>
        {
            IReadOnlyList<IntegrationConnectionSummary> list = await store
                .ListAsync(httpContext.RequestAborted)
                .ConfigureAwait(false);
            IReadOnlyList<ConnectionResponse> response = [.. list.Select(IntegrationContractMapper.ToResponse)];
            return Microsoft.AspNetCore.Http.Results.Ok(response);
        });

        connections.MapGet("/{id:guid}", async (HttpContext httpContext, IIntegrationConnectionStore store, Guid id) =>
        {
            IntegrationConnectionSummary? summary = await store
                .GetAsync(id, httpContext.RequestAborted)
                .ConfigureAwait(false);

            return summary is null
                ? CatalogResults.NotFound(httpContext, "The connection does not exist.")
                : Microsoft.AspNetCore.Http.Results.Ok(IntegrationContractMapper.ToResponse(summary));
        });

        connections.MapPost("/", async (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IIntegrationConnectionStore store,
            IIdentifierGenerator identifiers,
            IdempotencyPipeline idempotency,
            RegisterConnectionContract body) =>
        {
            IResult? preconditionFailure = await CheckMutationPreconditionsAsync(httpContext, antiforgery, body).ConfigureAwait(false);
            if (preconditionFailure is not null)
            {
                return preconditionFailure;
            }

            ConnectionRegistration registration;
            try
            {
                registration = IntegrationContractMapper.ToRegistration(body);
            }
            catch (CatalogContractFormatException exception)
            {
                return ValidationProblem(httpContext, exception.Message);
            }

            return await RunIdempotentAsync(
                httpContext,
                idempotency,
                operation: "RegisterConnection",
                payload: body,
                command: async token =>
                {
                    IntegrationConnectionSummary recorded = await store
                        .RecordAsync(identifiers.NewId(), registration, token)
                        .ConfigureAwait(false);
                    return Result.Success(recorded);
                },
                referenceFor: recorded => recorded.Id.ToString(),
                onExecuted: recorded => Microsoft.AspNetCore.Http.Results.Created(
                    $"{RoutePrefix}/connections/{recorded.Id}",
                    IntegrationContractMapper.ToResponse(recorded)),
                onReplayed: storedReference => ReplayedCreated($"{RoutePrefix}/connections", storedReference))
                .ConfigureAwait(false);
        });

        connections.MapPost("/{id:guid}/pause", (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IConnectionPauseStore pauseStore,
            IdempotencyPipeline idempotency,
            Guid id) =>
            RunConnectionTransitionAsync(
                httpContext,
                antiforgery,
                idempotency,
                "PauseConnection",
                id,
                token => pauseStore.PauseAsync(id, token)));

        connections.MapPost("/{id:guid}/resume", (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IConnectionPauseStore pauseStore,
            IdempotencyPipeline idempotency,
            Guid id) =>
            RunConnectionTransitionAsync(
                httpContext,
                antiforgery,
                idempotency,
                "ResumeConnection",
                id,
                token => pauseStore.ResumeAsync(id, token)));
    }

    private static void MapMappingEndpoints(RouteGroupBuilder group)
    {
        RouteGroupBuilder mappings = group.MapGroup("/mappings");

        mappings.MapGet("/", async (
            HttpContext httpContext,
            IEntityMappingStore store,
            string? platform,
            string? account,
            string? externalId) =>
        {
            if (string.IsNullOrWhiteSpace(platform)
                || string.IsNullOrWhiteSpace(account)
                || string.IsNullOrWhiteSpace(externalId))
            {
                return ValidationProblem(httpContext, "The 'platform', 'account', and 'externalId' query values are required.");
            }

            EntityMappingRecord? record = await store
                .FindAsync(platform, account, externalId, httpContext.RequestAborted)
                .ConfigureAwait(false);

            return record is null
                ? CatalogResults.NotFound(httpContext, "No mapping exists for that platform, account, and external identifier.")
                : Microsoft.AspNetCore.Http.Results.Ok(IntegrationContractMapper.ToResponse(record));
        });

        mappings.MapPost("/", async (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            IEntityMappingStore store,
            IdempotencyPipeline idempotency,
            CreateMappingContract body) =>
        {
            IResult? preconditionFailure = await CheckMutationPreconditionsAsync(httpContext, antiforgery, body).ConfigureAwait(false);
            if (preconditionFailure is not null)
            {
                return preconditionFailure;
            }

            EntityMappingRequest request = IntegrationContractMapper.ToRequest(body);

            return await RunIdempotentAsync(
                httpContext,
                idempotency,
                operation: "CreateMapping",
                payload: body,
                command: token => store.MapAsync(request, token),
                referenceFor: record => record.Id.ToString(),
                onExecuted: record => Microsoft.AspNetCore.Http.Results.Created(
                    $"{RoutePrefix}/mappings?platform={Uri.EscapeDataString(record.Platform)}&account={Uri.EscapeDataString(record.Account)}&externalId={Uri.EscapeDataString(record.ExternalId)}",
                    IntegrationContractMapper.ToResponse(record)),
                onReplayed: storedReference => Guid.TryParse(storedReference, out Guid mappingId)
                    ? Microsoft.AspNetCore.Http.Results.Ok(new { id = mappingId })
                    : Microsoft.AspNetCore.Http.Results.Ok())
                .ConfigureAwait(false);
        });
    }

    private static void MapSyncRunEndpoints(RouteGroupBuilder group)
    {
        // Sync history is read-only here: runs are recorded by the Background_Worker, which is
        // disabled for every connector in Phase A, so there is no Owner "start a run" command.
        group.MapGet("/sync-runs", async (HttpContext httpContext, SyncHistoryService service, Guid? connectionId) =>
        {
            if (connectionId is null)
            {
                return ValidationProblem(httpContext, "The 'connectionId' query value is required.");
            }

            SyncHistoryView? view = await service
                .GetViewAsync(connectionId.Value, httpContext.RequestAborted)
                .ConfigureAwait(false);

            return view is null
                ? CatalogResults.NotFound(httpContext, "The connection does not exist.")
                : Microsoft.AspNetCore.Http.Results.Ok(IntegrationContractMapper.ToResponse(view.Summary));
        });
    }

    private static void MapExceptionEndpoints(RouteGroupBuilder group)
    {
        RouteGroupBuilder exceptions = group.MapGroup("/exceptions");

        exceptions.MapGet("/", async (HttpContext httpContext, SyncHistoryService service, Guid? connectionId) =>
        {
            if (connectionId is null)
            {
                return ValidationProblem(httpContext, "The 'connectionId' query value is required.");
            }

            SyncHistoryView? view = await service
                .GetViewAsync(connectionId.Value, httpContext.RequestAborted)
                .ConfigureAwait(false);

            if (view is null)
            {
                return CatalogResults.NotFound(httpContext, "The connection does not exist.");
            }

            IReadOnlyList<SyncExceptionResponse> response = [.. view.Exceptions.Select(IntegrationContractMapper.ToResponse)];
            return Microsoft.AspNetCore.Http.Results.Ok(response);
        });

        exceptions.MapPost("/{id:guid}/retry", (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            SyncHistoryService service,
            IdempotencyPipeline idempotency,
            Guid id) =>
            RunExceptionActionAsync(httpContext, antiforgery, idempotency, service, "RetrySyncException", id, SyncExceptionAction.Retry));

        exceptions.MapPost("/{id:guid}/reconcile", (
            HttpContext httpContext,
            IAntiforgery antiforgery,
            SyncHistoryService service,
            IdempotencyPipeline idempotency,
            Guid id) =>
            RunExceptionActionAsync(httpContext, antiforgery, idempotency, service, "ReconcileSyncException", id, SyncExceptionAction.Reconcile));
    }

    private static async Task<IResult> RunConnectionTransitionAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        IdempotencyPipeline idempotency,
        string operation,
        Guid connectionId,
        System.Func<CancellationToken, Task<bool>> transition)
    {
        IResult? preconditionFailure = await CheckMutationPreconditionsAsync(httpContext, antiforgery, body: new object()).ConfigureAwait(false);
        if (preconditionFailure is not null)
        {
            return preconditionFailure;
        }

        return await RunIdempotentAsync(
            httpContext,
            idempotency,
            operation,
            payload: new { connectionId },
            command: async token =>
            {
                bool found = await transition(token).ConfigureAwait(false);
                return found
                    ? Result.Success(connectionId)
                    : Result.Failure<Guid>(ErrorCode.Validation, "The connection does not exist.");
            },
            referenceFor: id => id.ToString(),
            onExecuted: _ => Microsoft.AspNetCore.Http.Results.NoContent(),
            onReplayed: _ => Microsoft.AspNetCore.Http.Results.NoContent())
            .ConfigureAwait(false);
    }

    private static async Task<IResult> RunExceptionActionAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        IdempotencyPipeline idempotency,
        SyncHistoryService service,
        string operation,
        Guid exceptionId,
        SyncExceptionAction action)
    {
        IResult? preconditionFailure = await CheckMutationPreconditionsAsync(httpContext, antiforgery, body: new object()).ConfigureAwait(false);
        if (preconditionFailure is not null)
        {
            return preconditionFailure;
        }

        return await RunIdempotentAsync(
            httpContext,
            idempotency,
            operation,
            payload: new { exceptionId },
            command: async token =>
            {
                Result applied = await service.ApplyActionAsync(exceptionId, action, token).ConfigureAwait(false);
                return applied.IsSuccess
                    ? Result.Success(exceptionId)
                    : Result.Failure<Guid>(applied.Error);
            },
            referenceFor: id => id.ToString(),
            onExecuted: _ => Microsoft.AspNetCore.Http.Results.NoContent(),
            onReplayed: _ => Microsoft.AspNetCore.Http.Results.NoContent())
            .ConfigureAwait(false);
    }

    private static async Task<IResult?> CheckMutationPreconditionsAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        object? body)
    {
        IResult? csrfFailure = await AntiforgeryGuard.ValidateAsync(httpContext, antiforgery).ConfigureAwait(false);
        if (csrfFailure is not null)
        {
            return csrfFailure;
        }

        return body is null
            ? ValidationProblem(httpContext, "A request body is required.")
            : null;
    }

    private static async Task<IResult> RunIdempotentAsync<TValue>(
        HttpContext httpContext,
        IdempotencyPipeline idempotency,
        string operation,
        object payload,
        System.Func<CancellationToken, Task<Result<TValue>>> command,
        System.Func<TValue, string> referenceFor,
        System.Func<TValue, IResult> onExecuted,
        System.Func<string?, IResult> onReplayed)
    {
        if (!MutationHeaders.TryGetIdempotencyKey(httpContext, out string? key))
        {
            return ValidationProblem(httpContext, $"The '{MutationHeaders.IdempotencyKeyHeader}' header is required on a mutating request.");
        }

        ActorContext actor = ActorContextResolver.Resolve(httpContext.User);

        IdempotencyPipelineResult<Result<TValue>> outcome = await idempotency
            .ExecuteAsync(
                new IdempotencyKey(key!),
                Caller.FromActor(actor),
                new Operation(operation),
                PayloadHasher.Compute(System.Text.Json.JsonSerializer.Serialize(payload)),
                async token =>
                {
                    Result<TValue> result = await command(token).ConfigureAwait(false);
                    string reference = result.IsSuccess ? referenceFor(result.Value) : "rejected";
                    return new CommandExecution<Result<TValue>>(result, new ResultRef(reference));
                },
                httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (outcome.WasConflict)
        {
            return CatalogResults.Problem(outcome.Conflict!.Value, httpContext);
        }

        if (outcome.WasReplayed)
        {
            return onReplayed(outcome.StoredResult?.Value);
        }

        Result<TValue> executed = outcome.Value;
        return executed.IsFailure
            ? CatalogResults.Problem(executed.Error, httpContext)
            : onExecuted(executed.Value);
    }

    private static IResult ReplayedCreated(string collectionPath, string? storedReference)
    {
        if (!string.IsNullOrEmpty(storedReference) && Guid.TryParse(storedReference, out Guid id))
        {
            return Microsoft.AspNetCore.Http.Results.Created($"{collectionPath}/{id}", new { id });
        }

        return Microsoft.AspNetCore.Http.Results.Ok();
    }

    private static IResult ValidationProblem(HttpContext httpContext, string detail) =>
        CatalogResults.Problem(new BusinessError(ErrorCode.Validation, detail), httpContext);
}
