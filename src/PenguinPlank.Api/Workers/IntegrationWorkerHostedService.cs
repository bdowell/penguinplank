using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Integrations.Worker;

namespace PenguinPlank.Api.Workers;

/// <summary>
/// The Background_Worker: a hosted service that repeatedly runs one <see cref="WorkItemProcessor"/>
/// cycle per poll interval (requirement 3.15). It runs in the API process, so it does not rely on
/// SQL Server Agent.
/// </summary>
/// <remarks>
/// Each cycle gets its own DI scope, so the scoped database context is never captured by this
/// singleton. The scope's actor is set to <see cref="ActorContext.SystemWorker"/> before any work,
/// so every worker mutation is audited with the system identity (requirement A7 9.6). A failed
/// cycle is logged and the loop continues; the unfinished lease expires and is reclaimed, which
/// keeps processing crash-safe.
/// </remarks>
public sealed partial class IntegrationWorkerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<IntegrationWorkerHostedService> _logger;
    private readonly string _leaseOwner;

    /// <summary>Creates the hosted service.</summary>
    /// <param name="scopeFactory">Creates the per-cycle scope.</param>
    /// <param name="timeProvider">The time source used to wait between cycles.</param>
    /// <param name="options">The worker settings; validated here.</param>
    /// <param name="logger">The logger.</param>
    public IntegrationWorkerHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        WorkerOptions options,
        ILogger<IntegrationWorkerHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        options.Validate();

        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
        _leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    }

    /// <summary>Runs one cycle in a fresh scope with the system actor.</summary>
    /// <param name="cancellationToken">A token to cancel the cycle.</param>
    /// <returns>The counts for the cycle.</returns>
    public async Task<WorkBatchResult> RunCycleAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            scope.ServiceProvider
                .GetRequiredService<IActorContextAccessor>()
                .SetCurrent(ActorContext.SystemWorker());

            WorkItemProcessor processor = scope.ServiceProvider.GetRequiredService<WorkItemProcessor>();

            return await processor.ProcessBatchAsync(_leaseOwner, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                WorkBatchResult result = await RunCycleAsync(stoppingToken).ConfigureAwait(false);
                LogCycleCompleted(result.Leased, result.Succeeded, result.Rescheduled, result.Released);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogCycleFailed(exception);
            }

            try
            {
                await Task.Delay(_options.PollInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Worker cycle: leased {Leased}, succeeded {Succeeded}, rescheduled {Rescheduled}, released {Released}.")]
    private partial void LogCycleCompleted(int leased, int succeeded, int rescheduled, int released);

    [LoggerMessage(Level = LogLevel.Error, Message = "Worker cycle failed; leases will expire and be reclaimed.")]
    private partial void LogCycleFailed(Exception exception);
}
