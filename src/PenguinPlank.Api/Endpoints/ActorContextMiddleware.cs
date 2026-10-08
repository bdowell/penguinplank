using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;

namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Publishes the authenticated request's <see cref="ActorContext"/> into the scoped
/// <see cref="IActorContextAccessor"/> so the audit <c>SaveChanges</c> interceptor can attribute a
/// business mutation to its actor without the Application or Domain layer touching
/// <c>HttpContext</c> (coding-standards §2, §3; requirements A2 §5.12, A4 §6.7).
/// </summary>
/// <remarks>
/// <para>
/// The audit interceptor reads the actor for the current unit of work from the scoped accessor and
/// refuses to persist a business mutation that has none. The endpoints resolve the actor at the
/// boundary and pass it <em>explicitly</em> into use cases, but the automatic interceptor runs at
/// the persistence seam below those calls, so it needs the actor established on the per-request
/// scope. This middleware is that single boundary step: it runs after authentication (so the
/// principal is resolved) and, for an authenticated request, maps the principal to an
/// <see cref="ActorContext"/> through the same pure <see cref="ActorContextResolver"/> the handlers
/// use and records it on the scoped accessor.
/// </para>
/// <para>
/// Anonymous requests (the login/CSRF endpoints and the first-run Owner bootstrap) carry no
/// principal, so this middleware sets nothing for them: a safe read performs no mutation, and the
/// one anonymous mutation — the Owner bootstrap — is a system action that records the
/// <see cref="ActorContext.SystemWorker"/> identity itself at its own boundary. The middleware
/// never overwrites an actor another boundary already set.
/// </para>
/// </remarks>
public sealed class ActorContextMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware over the next pipeline component.</summary>
    /// <param name="next">The next request delegate in the pipeline.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="next"/> is <see langword="null"/>.</exception>
    public ActorContextMiddleware(RequestDelegate next)
    {
        System.ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>
    /// Records the authenticated actor on the scoped accessor, then invokes the rest of the
    /// pipeline.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="actorContextAccessor">The scoped accessor the audit interceptor reads.</param>
    /// <returns>A task that completes when the pipeline has finished.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when a dependency is <see langword="null"/>.</exception>
    public async Task InvokeAsync(HttpContext httpContext, IActorContextAccessor actorContextAccessor)
    {
        System.ArgumentNullException.ThrowIfNull(httpContext);
        System.ArgumentNullException.ThrowIfNull(actorContextAccessor);

        if (actorContextAccessor.Current is null
            && httpContext.User.Identity is { IsAuthenticated: true })
        {
            actorContextAccessor.SetCurrent(ActorContextResolver.Resolve(httpContext.User));
        }

        await _next(httpContext).ConfigureAwait(false);
    }
}
