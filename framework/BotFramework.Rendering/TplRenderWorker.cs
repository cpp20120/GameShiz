using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotFramework.Rendering;

internal sealed partial class TplRenderWorker : IRenderQueue, IRenderHistory, IHostedService, IDisposable
{
    private readonly IServiceScopeFactory scopes;
    private readonly IRenderArtifactStore store;
    private readonly RenderingOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<TplRenderWorker> logger;
    private readonly ConcurrentDictionary<string, Lazy<Task<RenderedArtifact>>> inFlight = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource stopping = new();
    private readonly SemaphoreSlim renderSlots;
    private readonly ActionBlock<RenderWorkItem> interactive;
    private readonly ActionBlock<RenderWorkItem> background;

    public TplRenderWorker(
        IServiceScopeFactory scopes,
        IRenderArtifactStore store,
        IOptions<RenderingOptions> options,
        TimeProvider timeProvider,
        ILogger<TplRenderWorker> logger)
    {
        this.scopes = scopes;
        this.store = store;
        this.options = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
        renderSlots = new SemaphoreSlim(this.options.EffectiveParallelism);

        interactive = CreateBlock(Math.Max(1, this.options.QueueCapacity / 2));
        background = CreateBlock(Math.Max(1, this.options.QueueCapacity));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        LogStarted(options.QueueCapacity, options.EffectiveParallelism);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        interactive.Complete();
        background.Complete();
        try
        {
            await Task.WhenAll(interactive.Completion, background.Completion)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host shutdown deadline owns cancellation here.
        }
    }

    public void Dispose()
    {
        renderSlots.Dispose();
        stopping.Dispose();
    }

    public async ValueTask<RenderedArtifact> GetOrRenderAsync<TSpec>(
        TSpec spec,
        RenderPriority priority = RenderPriority.Interactive,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var key = Describe(spec);
        var candidate = new Lazy<Task<RenderedArtifact>>(
            () => EnqueueCoreAsync(spec, key, priority),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var actual = inFlight.GetOrAdd(key.Value, candidate);
        try
        {
            return await actual.Value.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            if (actual.IsValueCreated && actual.Value.IsCompleted)
                inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<RenderedArtifact>>>(key.Value, actual));
        }
    }

    public Task PrewarmAsync<TSpec>(IEnumerable<TSpec> specs, CancellationToken ct = default) =>
        Task.WhenAll(specs.Select(spec =>
            GetOrRenderAsync(spec, RenderPriority.Prewarm, ct).AsTask()));

    public async ValueTask RecordAsync(RenderHistoryEntry entry, CancellationToken ct = default)
    {
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await store.RecordHistoryAsync(entry, ct).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < attempts)
            {
                RenderTelemetry.StoreFailed(entry.GameId, "history");
                LogHistoryRetry(entry.GameId, entry.AggregateId, attempt, ex);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), timeProvider, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Media history must not turn an already committed game into a failed command.
                // A later game render/cache hit may record another manifest for the artifact.
                RenderTelemetry.StoreFailed(entry.GameId, "history");
                LogHistoryDropped(entry.GameId, entry.AggregateId, ex);
            }
        }
    }

    public IAsyncEnumerable<RenderHistoryEntry> ListAsync(
        string gameId,
        string aggregateId,
        int take = 50,
        CancellationToken ct = default) =>
        store.ListHistoryAsync(gameId, aggregateId, take, ct);

    private RenderKey Describe<TSpec>(TSpec spec)
    {
        using var scope = scopes.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IRenderJob<TSpec>>().Describe(spec);
    }

    private async Task<RenderedArtifact> EnqueueCoreAsync<TSpec>(
        TSpec spec,
        RenderKey key,
        RenderPriority priority)
    {
        var cached = await FindCachedAsync(key).ConfigureAwait(false);
        if (cached is not null)
        {
            RenderTelemetry.Hit(key.RendererId);
            return cached;
        }

        RenderTelemetry.Miss(key.RendererId);
        var completion = new TaskCompletionSource<RenderedArtifact>(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new RenderWorkItem(
            key,
            async ct =>
            {
                await using var scope = scopes.CreateAsyncScope();
                var job = scope.ServiceProvider.GetRequiredService<IRenderJob<TSpec>>();
                var output = await job.RenderAsync(spec, ct).ConfigureAwait(false);
                if (output.Content.LongLength > options.MaxArtifactBytes)
                    throw new InvalidOperationException(
                        $"Renderer '{key.RendererId}' produced {output.Content.LongLength} bytes; limit is {options.MaxArtifactBytes}.");
                try
                {
                    return await store.PutAsync(key, output, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // The render itself is still usable for the current Telegram response.
                    // MinIO is an artifact cache/history store, not part of the game commit.
                    RenderTelemetry.StoreFailed(key.RendererId, "write");
                    LogStoreWriteFailed(key.Value, ex);
                    return new RenderedArtifact(
                        key,
                        output.Content,
                        output.FileName ?? $"render.{key.Extension.TrimStart('.')}",
                        timeProvider.GetUtcNow(),
                        key.ObjectName,
                        CacheHit: false);
                }
            },
            completion);
        var block = priority == RenderPriority.Interactive ? interactive : background;
        RenderTelemetry.Enqueued(key.RendererId);
        if (!await block.SendAsync(item, stopping.Token).ConfigureAwait(false))
            throw new InvalidOperationException("The render queue is no longer accepting work.");
        return await completion.Task.ConfigureAwait(false);
    }

    private ActionBlock<RenderWorkItem> CreateBlock(int capacity) => new(
        ExecuteAsync,
        new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = capacity,
            CancellationToken = stopping.Token,
            EnsureOrdered = false,
            MaxDegreeOfParallelism = options.EffectiveParallelism,
        });

    private async Task ExecuteAsync(RenderWorkItem item)
    {
        RenderTelemetry.Dequeued(item.Key.RendererId);
        var started = timeProvider.GetTimestamp();
        var slotAcquired = false;
        try
        {
            await renderSlots.WaitAsync(stopping.Token).ConfigureAwait(false);
            slotAcquired = true;
            var artifact = await item.Execute(stopping.Token).ConfigureAwait(false);
            item.Completion.TrySetResult(artifact);
            RenderTelemetry.Completed(item.Key.RendererId, timeProvider.GetElapsedTime(started));
        }
        catch (OperationCanceledException ex) when (stopping.IsCancellationRequested)
        {
            item.Completion.TrySetCanceled(ex.CancellationToken);
        }
        catch (Exception ex)
        {
            RenderTelemetry.Failed(item.Key.RendererId);
            LogFailed(item.Key.Value, ex);
            item.Completion.TrySetException(ex);
        }
        finally
        {
            if (slotAcquired)
                renderSlots.Release();
        }
    }

    private async Task<RenderedArtifact?> FindCachedAsync(RenderKey key)
    {
        try
        {
            return await store.FindAsync(key, stopping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            RenderTelemetry.StoreFailed(key.RendererId, "read");
            LogStoreReadFailed(key.Value, ex);
            return null;
        }
    }

    private sealed record RenderWorkItem(
        RenderKey Key,
        Func<CancellationToken, Task<RenderedArtifact>> Execute,
        TaskCompletionSource<RenderedArtifact> Completion);

    [LoggerMessage(EventId = 6110, Level = LogLevel.Information, Message = "render.worker started capacity={Capacity} parallelism={Parallelism}")]
    private partial void LogStarted(int capacity, int parallelism);

    [LoggerMessage(EventId = 6111, Level = LogLevel.Error, Message = "render.worker failed key={RenderKey}")]
    private partial void LogFailed(string renderKey, Exception exception);

    [LoggerMessage(EventId = 6112, Level = LogLevel.Warning, Message = "render.store read failed key={RenderKey}; rendering without cache")]
    private partial void LogStoreReadFailed(string renderKey, Exception exception);

    [LoggerMessage(EventId = 6113, Level = LogLevel.Warning, Message = "render.store write failed key={RenderKey}; returning transient artifact")]
    private partial void LogStoreWriteFailed(string renderKey, Exception exception);

    [LoggerMessage(EventId = 6114, Level = LogLevel.Warning, Message = "render.history write retry game={GameId} aggregate={AggregateId} attempt={Attempt}")]
    private partial void LogHistoryRetry(string gameId, string aggregateId, int attempt, Exception exception);

    [LoggerMessage(EventId = 6115, Level = LogLevel.Error, Message = "render.history write dropped game={GameId} aggregate={AggregateId}; game command remains successful")]
    private partial void LogHistoryDropped(string gameId, string aggregateId, Exception exception);
}
