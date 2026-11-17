using Microsoft.Extensions.DependencyInjection.Extensions;
using PenguinPlank.Api.Workers;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Application.Integrations.Worker;
using PenguinPlank.Infrastructure.Integrations;

namespace PenguinPlank.Api.Composition;

/// <summary>
/// Registers the Background_Worker (requirements 3.15, 3.16, 3.17). The dispatcher and HTTP
/// transport are the disabled Phase A implementations, so no job reaches a real provider.
/// </summary>
public static class IntegrationWorkerServiceCollectionExtensions
{
    /// <summary>The configuration section that overrides <see cref="WorkerOptions"/>.</summary>
    public const string WorkerSectionName = "IntegrationWorker";

    /// <summary>The configuration section that overrides <see cref="RetryBackoffOptions"/>.</summary>
    public const string RetrySectionName = "IntegrationRetry";

    private static readonly RetryBackoffOptions s_defaultRetryOptions = new(
        MaxAttempts: 5,
        InitialDelay: TimeSpan.FromSeconds(30),
        MaxDelay: TimeSpan.FromMinutes(30),
        JitterRatio: 0.2,
        MinimumInterval: TimeSpan.FromSeconds(1));

    /// <summary>Registers the worker services and, when a database is configured, the hosted service.</summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddPenguinPlankIntegrationWorker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        WorkerOptions workerOptions = configuration.GetSection(WorkerSectionName).Get<WorkerOptions>()
            ?? WorkerOptions.Default;
        RetryBackoffOptions retryOptions = configuration.GetSection(RetrySectionName).Get<RetryBackoffOptions>()
            ?? s_defaultRetryOptions;

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(workerOptions);
        services.TryAddSingleton<IRetryScheduler>(
            _ => new BackoffRetryScheduler(retryOptions, _ => Random.Shared.NextDouble()));
        services.TryAddSingleton<IIntegrationHttpTransport, DisabledIntegrationHttpTransport>();
        services.TryAddSingleton<IWorkItemDispatcher, DisabledConnectorDispatcher>();
        services.TryAddScoped<IWorkLeaseStore, EfWorkLeaseStore>();
        services.TryAddScoped<WorkItemProcessor>();

        // Without a database there is nothing to lease; skip the loop so a build-only host starts.
        if (!string.IsNullOrWhiteSpace(
                configuration.GetConnectionString(ServiceCollectionExtensions.DatabaseConnectionName)))
        {
            services.AddHostedService<IntegrationWorkerHostedService>();
        }

        return services;
    }
}
