using Microsoft.Extensions.DependencyInjection.Extensions;
using PenguinPlank.Application.Idempotency;
using PenguinPlank.Infrastructure.Idempotency;
using PenguinPlank.Infrastructure.Persistence;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Composition-root registrations for the idempotency pipeline and store (requirement A4
/// §6.2–§6.4).
/// </summary>
/// <remarks>
/// <para>
/// This lives in its own file and extension method, additive to
/// <see cref="ServiceCollectionExtensions.AddPenguinPlankServices"/>, so the idempotency wiring
/// stays localized and does not contend with the persistence/identity, authorization, or
/// Owner-bootstrap registrations added by parallel foundation tasks. It is still part of the one
/// composition root: no business code resolves these services at runtime (coding-standards §2).
/// </para>
/// <para>
/// <b>Lifetimes.</b> The <see cref="EfIdempotencyStore"/> is registered <b>scoped</b> because it
/// captures the scoped <see cref="PenguinPlankDbContext"/>; a singleton would illegally capture a
/// scoped context (requirement A2 §5.14). The <see cref="IdempotencyPipeline"/> is scoped too, as
/// it depends on the scoped store. <see cref="System.TimeProvider"/> is registered as a singleton
/// only if nothing else already registered one, so the shared clock convention
/// (<c>TimeConvention</c>; requirement A8 §10.1) has a single source without conflicting with a
/// parallel task that also needs it.
/// </para>
/// </remarks>
public static class IdempotencyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the idempotency store, pipeline, and the shared <see cref="System.TimeProvider"/>.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddPenguinPlankIdempotency(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        // Shared clock. TryAdd so this coexists with any other foundation task that registers the
        // same system TimeProvider; the first registration wins and both get the same instance.
        services.TryAddSingleton(System.TimeProvider.System);

        // Scoped: the store captures the scoped DbContext (A2 §5.14). Registered for both the
        // boundary interface and the concrete type so either can be resolved.
        services.AddScoped<EfIdempotencyStore>();
        services.AddScoped<IIdempotencyStore>(provider =>
            provider.GetRequiredService<EfIdempotencyStore>());

        // Scoped: the pipeline depends on the scoped store.
        services.AddScoped<IdempotencyPipeline>();

        return services;
    }
}
