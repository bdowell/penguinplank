using Microsoft.Extensions.DependencyInjection.Extensions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Infrastructure.Auditing;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Composition-root registrations for the audit trail (task 6.7; requirements A2 §5.12, A4 §6.7,
/// A7 §9.6).
/// </summary>
/// <remarks>
/// <para>
/// This wiring is isolated in its own extension, additive to
/// <see cref="ServiceCollectionExtensions.AddPenguinPlankServices"/>, so the audit registrations
/// stay localized alongside the other foundation extensions. It remains part of the one
/// composition root: no business code resolves these services at runtime (coding-standards §2).
/// </para>
/// <para>
/// <b>Lifetimes (requirement A2 §5.14).</b>
/// </para>
/// <list type="bullet">
///   <item><description>
///     <see cref="IActorContextAccessor"/> → <see cref="AmbientActorContextAccessor"/> is
///     <b>scoped</b>: one actor per request scope / background-worker job scope, matching the
///     scoped <c>DbContext</c> the interceptor observes. The API boundary sets it per request; the
///     worker sets it to the system actor.
///   </description></item>
///   <item><description>
///     <see cref="AuditSaveChangesInterceptor"/> is <b>scoped</b> because it depends on the scoped
///     actor accessor. It is attached to the scoped <c>DbContext</c> through the service-provider
///     overload of <c>AddDbContext</c> in <see cref="ServiceCollectionExtensions"/>, so no singleton
///     captures a scoped dependency.
///   </description></item>
///   <item><description>
///     <see cref="IAuditSink"/> → <see cref="EfAuditSink"/> is <b>scoped</b> because it captures the
///     scoped <c>DbContext</c>.
///   </description></item>
///   <item><description>
///     <see cref="System.TimeProvider"/> is added only if nothing else registered it, sharing the
///     one clock convention (requirement A8 §10.1).
///   </description></item>
/// </list>
/// </remarks>
public static class AuditingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the actor-context accessor, the audit <c>SaveChanges</c> interceptor, and the audit
    /// sink. The interceptor is attached to the <c>DbContext</c> options in
    /// <see cref="ServiceCollectionExtensions"/>.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddPenguinPlankAuditing(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        // Shared clock. TryAdd so this coexists with any other foundation task that registers the
        // same system TimeProvider; the first registration wins and both get the same instance.
        services.TryAddSingleton(System.TimeProvider.System);

        // Scoped: one actor per request / worker-job scope, matching the scoped DbContext.
        services.AddScoped<AmbientActorContextAccessor>();
        services.AddScoped<IActorContextAccessor>(provider =>
            provider.GetRequiredService<AmbientActorContextAccessor>());

        // Scoped: the interceptor depends on the scoped actor accessor. It is attached to the
        // scoped DbContext via the service-provider overload of AddDbContext (A2 §5.14).
        services.AddScoped<AuditSaveChangesInterceptor>();

        // Scoped: the sink captures the scoped DbContext (A2 §5.14).
        services.AddScoped<EfAuditSink>();
        services.AddScoped<IAuditSink>(provider => provider.GetRequiredService<EfAuditSink>());

        return services;
    }
}
