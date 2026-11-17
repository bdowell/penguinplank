using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PenguinPlank.Api.Workers;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Application.Integrations.Worker;

namespace UnitTests.Integrations;

/// <summary>
/// Tests that the Background_Worker runs every cycle in its own scope under the recorded system
/// actor (requirement A7 9.6), so each lease and outcome write is attributed to the worker.
/// </summary>
public class IntegrationWorkerHostedServiceTests
{
    private static readonly DateTimeOffset s_start = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunCycleAsync_LeasedItem_LeaseDispatchAndOutcomeAllSeeTheSystemActorAsync()
    {
        LeasedWorkItem item = new(WorkItemKind.Outbox, Guid.NewGuid(), Guid.NewGuid(), "payload-1", 1);
        var stores = new List<RecordingWorkLeaseStore>();
        var dispatchers = new List<ScriptedDispatcher>();
        await using ServiceProvider provider = BuildProvider(
            scope =>
            {
                var store = new RecordingWorkLeaseStore([item], () => CurrentActor(scope));
                stores.Add(store);
                return store;
            },
            scope =>
            {
                var dispatcher = new ScriptedDispatcher(_ => DispatchOutcome.Succeeded, () => CurrentActor(scope));
                dispatchers.Add(dispatcher);
                return dispatcher;
            });

        WorkBatchResult result = await CreateService(provider).RunCycleAsync(CancellationToken.None);

        Assert.Equal(new WorkBatchResult(1, 1, 0, 0), result);
        RecordingWorkLeaseStore store = Assert.Single(stores);
        Assert.All(store.Leases, call => AssertSystemWorker(call.Actor));
        OutcomeCall outcome = Assert.Single(store.Outcomes);
        AssertSystemWorker(outcome.Actor);
        AssertSystemWorker(Assert.Single(Assert.Single(dispatchers).ActorsSeen));
    }

    [Fact]
    public async Task RunCycleAsync_TwoCycles_UseFreshScopesAndTheSameLeaseOwnerAsync()
    {
        var stores = new List<RecordingWorkLeaseStore>();
        await using ServiceProvider provider = BuildProvider(
            scope =>
            {
                var store = new RecordingWorkLeaseStore([]);
                stores.Add(store);
                return store;
            },
            _ => new ScriptedDispatcher(_ => DispatchOutcome.Succeeded));
        IntegrationWorkerHostedService service = CreateService(provider);

        await service.RunCycleAsync(CancellationToken.None);
        await service.RunCycleAsync(CancellationToken.None);

        Assert.Equal(2, stores.Count);
        string firstOwner = Assert.Single(stores[0].Leases).LeaseOwner;
        string secondOwner = Assert.Single(stores[1].Leases).LeaseOwner;
        Assert.False(string.IsNullOrWhiteSpace(firstOwner));
        Assert.Equal(firstOwner, secondOwner);
    }

    [Fact]
    public async Task Constructor_InvalidOptions_ThrowsAsync()
    {
        await using ServiceProvider provider = BuildProvider(
            _ => new RecordingWorkLeaseStore([]),
            _ => new ScriptedDispatcher(_ => DispatchOutcome.Succeeded));

        Assert.Throws<ArgumentOutOfRangeException>(() => new IntegrationWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeTimeProvider(s_start),
            new WorkerOptions(TimeSpan.Zero, TimeSpan.FromMinutes(5), 10),
            NullLogger<IntegrationWorkerHostedService>.Instance));
    }

    private static ActorContext? CurrentActor(IServiceProvider scope)
    {
        return scope.GetRequiredService<IActorContextAccessor>().Current;
    }

    private static void AssertSystemWorker(ActorContext? actor)
    {
        Assert.NotNull(actor);
        Assert.True(actor.IsSystemWorker);
        Assert.Equal(ActorContext.SystemWorkerId, actor.UserId);
    }

    private static IntegrationWorkerHostedService CreateService(ServiceProvider provider)
    {
        return new IntegrationWorkerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeTimeProvider(s_start),
            WorkerOptions.Default,
            NullLogger<IntegrationWorkerHostedService>.Instance);
    }

    private static ServiceProvider BuildProvider(
        Func<IServiceProvider, RecordingWorkLeaseStore> createStore,
        Func<IServiceProvider, ScriptedDispatcher> createDispatcher)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(s_start));
        services.AddSingleton(WorkerOptions.Default);
        services.AddSingleton<IRetryScheduler>(new FixedRetryScheduler(3, TimeSpan.FromMinutes(1)));
        services.AddScoped<IActorContextAccessor, AmbientActorContextAccessor>();
        services.AddScoped<IWorkLeaseStore>(createStore);
        services.AddScoped<IWorkItemDispatcher>(createDispatcher);
        services.AddScoped<WorkItemProcessor>();
        return services.BuildServiceProvider();
    }
}
