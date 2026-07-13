using BotFramework.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class RenderingWorkerTests
{
    [Fact]
    public async Task SameContent_IsRenderedOnce_AndThenReadFromCache()
    {
        await using var provider = CreateProvider(maxParallelism: 2);
        var queue = provider.GetRequiredService<IRenderQueue>();
        var probe = provider.GetRequiredService<RenderProbe>();
        var spec = new TestRenderSpec("same", DelayMilliseconds: 25);

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => queue.GetOrRenderAsync(spec).AsTask()));

        Assert.Equal(1, probe.RenderCount);
        Assert.All(concurrent, artifact => Assert.Equal("same", System.Text.Encoding.UTF8.GetString(artifact.Content)));

        var cached = await queue.GetOrRenderAsync(spec);
        Assert.True(cached.CacheHit);
        Assert.Equal(1, probe.RenderCount);
    }

    [Fact]
    public async Task InteractiveAndBackgroundQueues_ShareOneParallelismLimit()
    {
        await using var provider = CreateProvider(maxParallelism: 2);
        var queue = provider.GetRequiredService<IRenderQueue>();
        var probe = provider.GetRequiredService<RenderProbe>();

        var renders = Enumerable.Range(0, 12)
            .Select(index => queue.GetOrRenderAsync(
                new TestRenderSpec($"render-{index}", DelayMilliseconds: 60),
                index % 2 == 0 ? RenderPriority.Interactive : RenderPriority.Background)
                .AsTask());

        await Task.WhenAll(renders);

        Assert.Equal(12, probe.RenderCount);
        Assert.InRange(probe.MaxActive, 1, 2);
    }

    [Fact]
    public async Task History_CanBeReadByGameAndAggregate()
    {
        await using var provider = CreateProvider(maxParallelism: 1);
        var queue = provider.GetRequiredService<IRenderQueue>();
        var history = provider.GetRequiredService<IRenderHistory>();
        var artifact = await queue.GetOrRenderAsync(new TestRenderSpec("history", 0));
        var entry = new RenderHistoryEntry(
            "horse",
            "chat-42",
            "race-7",
            artifact.Key,
            DateTimeOffset.Parse("2026-07-13T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            new Dictionary<string, string> { ["winner"] = "3" });

        await history.RecordAsync(entry);
        RenderHistoryEntry? actual = null;
        await foreach (var item in history.ListAsync("horse", "chat-42"))
        {
            actual = item;
            break;
        }

        Assert.Equal(entry, actual);
    }

    [Fact]
    public async Task ArtifactStoreFailure_ReturnsTransientRender_AndDoesNotFailHistoryCall()
    {
        await using var provider = CreateProvider(maxParallelism: 1, failingStore: true);
        var queue = provider.GetRequiredService<IRenderQueue>();
        var history = provider.GetRequiredService<IRenderHistory>();

        var artifact = await queue.GetOrRenderAsync(new TestRenderSpec("transient", 0));
        await history.RecordAsync(new RenderHistoryEntry(
            "test",
            "aggregate",
            "match",
            artifact.Key,
            DateTimeOffset.UtcNow));

        Assert.False(artifact.CacheHit);
        Assert.Equal("transient", System.Text.Encoding.UTF8.GetString(artifact.Content));
    }

    private static ServiceProvider CreateProvider(int maxParallelism, bool failingStore = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rendering:QueueCapacity"] = "16",
                ["Rendering:MaxParallelism"] = maxParallelism.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Rendering:Minio:Enabled"] = "false",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<RenderProbe>();
        services.AddBotFrameworkRendering(configuration);
        if (failingStore)
            services.AddSingleton<IRenderArtifactStore, FailingArtifactStore>();
        services.AddRenderJob<TestRenderSpec, TestRenderJob>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    public sealed record TestRenderSpec(string Content, int DelayMilliseconds);

    public sealed class TestRenderJob(RenderProbe probe) : IRenderJob<TestRenderSpec>
    {
        public RenderKey Describe(TestRenderSpec spec) =>
            new("test", "1", spec.Content, "bin", "application/octet-stream");

        public async ValueTask<RenderOutput> RenderAsync(TestRenderSpec spec, CancellationToken ct)
        {
            probe.Enter();
            try
            {
                await Task.Delay(spec.DelayMilliseconds, ct);
                return RenderOutput.FromBytes(System.Text.Encoding.UTF8.GetBytes(spec.Content));
            }
            finally
            {
                probe.Exit();
            }
        }
    }

    public sealed class RenderProbe
    {
        private int active;
        private int maxActive;
        private int renderCount;

        public int MaxActive => Volatile.Read(ref maxActive);
        public int RenderCount => Volatile.Read(ref renderCount);

        public void Enter()
        {
            Interlocked.Increment(ref renderCount);
            var current = Interlocked.Increment(ref active);
            var observed = Volatile.Read(ref maxActive);
            while (current > observed)
            {
                var previous = Interlocked.CompareExchange(ref maxActive, current, observed);
                if (previous == observed)
                    break;
                observed = previous;
            }
        }

        public void Exit() => Interlocked.Decrement(ref active);
    }

    private sealed class FailingArtifactStore : IRenderArtifactStore
    {
        public ValueTask<RenderedArtifact?> FindAsync(RenderKey key, CancellationToken ct) =>
            ValueTask.FromException<RenderedArtifact?>(new IOException("store unavailable"));

        public ValueTask<RenderedArtifact> PutAsync(RenderKey key, RenderOutput output, CancellationToken ct) =>
            ValueTask.FromException<RenderedArtifact>(new IOException("store unavailable"));

        public ValueTask RecordHistoryAsync(RenderHistoryEntry entry, CancellationToken ct) =>
            ValueTask.FromException(new IOException("store unavailable"));

        public async IAsyncEnumerable<RenderHistoryEntry> ListHistoryAsync(
            string gameId,
            string aggregateId,
            int take,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield break;
        }
    }
}
