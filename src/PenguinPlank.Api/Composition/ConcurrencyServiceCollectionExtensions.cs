using PenguinPlank.Application.Abstractions.Concurrency;
using PenguinPlank.Infrastructure.Persistence.Concurrency;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the ETag / <c>If-Match</c> optimistic-concurrency boundary (task 6.5) into the
/// dependency-injection container.
/// </summary>
/// <remarks>
/// This wiring is isolated in its own extension so the single composition root
/// (<see cref="ServiceCollectionExtensions.AddPenguinPlankServices"/>) stays a thin list of
/// one-line calls and parallel foundation tasks do not contend over one method body.
/// </remarks>
public static class ConcurrencyServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="IConcurrentUpdateExecutor"/> to its EF Core implementation.
    /// </summary>
    /// <remarks>
    /// The executor is registered <b>scoped</b> because <see cref="EfConcurrentUpdateExecutor"/>
    /// captures the scoped <see cref="PenguinPlank.Infrastructure.Persistence.PenguinPlankDbContext"/>;
    /// a singleton would capture a scoped context and is forbidden (coding-standards §2;
    /// requirement A2 §5.14). The <c>If-Match</c> header itself is wired into endpoints in task 9.1.
    /// </remarks>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance, enabling fluent chaining.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddPenguinPlankConcurrency(this IServiceCollection services)
    {
        System.ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IConcurrentUpdateExecutor, EfConcurrentUpdateExecutor>();

        return services;
    }
}
