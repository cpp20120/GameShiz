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
    private readonly IServiceScopeFactory _scopes;
    private readonly IRenderArtifactStore _store;
    private readonly RenderingOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TplRenderWorker> _logger;
    private readonly ConcurrentDictionary<string, Lazy<Task<RenderedArtifact>>> _inFlight = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _renderSlots;
    private readonly ActionBlock<RenderWorkItem> _interactive;
    private readonly ActionBlock<RenderWorkItem> _background;

    public TplRenderWorker(
        IServiceScopeFactory scopes,
        IRenderArtifactStore store,
        IOptions<RenderingOptions> options,
        TimeProvider timeProvider,
        ILogger<TplRenderWorker> logger)
    {
        _scopes = scopes;
        _store = store;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _renderSlots = new SemaphoreSlim(_options.EffectiveParallelism);

        _interactive = CreateBlock(Math.Max(1, _options.QueueCapacity / 2));
        _background = CreateBlock(Math.Max(1, _options.QueueCapacity));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        LogStarted(_options.QueueCapacity, _options.EffectiveParallelism);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        _interactive.Complete();
        _background.Complete();
        try
        {
            await Task.WhenAll(_interactive.Completion, _background.Completion)
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _stopping.IsCancellationRequested)
        {
            // Queue cancellation is expected during normal shutdown.
        }
    }

    public void Dispose()
    {
        _renderSlots.Dispose();
        _stopping.Dispose();
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
        var actual = _inFlight.GetOrAdd(key.Value, candidate);
        try
        {
            return await actual.Value.WaitAsync(ct);
        }
        finally
        {
            if (actual.IsValueCreated && actual.Value.IsCompleted)
                _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<RenderedArtifact>>>(key.Value, actual));
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
                await _store.RecordHistoryAsync(entry, ct);
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
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), _timeProvider, ct);
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
        _store.ListHistoryAsync(gameId, aggregateId, take, ct);

    private RenderKey Describe<TSpec>(TSpec spec)
    {
        using var scope = _scopes.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IRenderJob<TSpec>>().Describe(spec);
    }

    private async Task<RenderedArtifact> EnqueueCoreAsync<TSpec>(
        TSpec spec,
        RenderKey key,
        RenderPriority priority)
    {
        var cached = await FindCachedAsync(key);
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
                await using var scope = _scopes.CreateAsyncScope();
                var job = scope.ServiceProvider.GetRequiredService<IRenderJob<TSpec>>();
                var output = await job.RenderAsync(spec, ct);
                if (output.Content.LongLength > _options.MaxArtifactBytes)
                {
                    throw new InvalidOperationException(
                        $"Renderer '{key.RendererId}' produced {output.Content.LongLength} bytes; limit is {_options.MaxArtifactBytes}.");
                }

                try
                {
                    return await _store.PutAsync(key, output, ct);
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
                        _timeProvider.GetUtcNow(),
                        key.ObjectName,
                        CacheHit: false);
                }
            },
            completion);
        var block = priority == RenderPriority.Interactive ? _interactive : _background;
        RenderTelemetry.Enqueued(key.RendererId);
        if (!await block.SendAsync(item, _stopping.Token))
            throw new InvalidOperationException("The render queue is no longer accepting work.");
        return await completion.Task;
    }

    private ActionBlock<RenderWorkItem> CreateBlock(int capacity) => new(
        ExecuteAsync,
        new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = capacity,
            CancellationToken = _stopping.Token,
            EnsureOrdered = false,
            MaxDegreeOfParallelism = _options.EffectiveParallelism,
        });

    private async Task ExecuteAsync(RenderWorkItem item)
    {
        RenderTelemetry.Dequeued(item.Key.RendererId);
        var started = _timeProvider.GetTimestamp();
        var slotAcquired = false;
        try
        {
            await _renderSlots.WaitAsync(_stopping.Token);
            slotAcquired = true;
            var artifact = await item.Execute(_stopping.Token);
            item.Completion.TrySetResult(artifact);
            RenderTelemetry.Completed(item.Key.RendererId, _timeProvider.GetElapsedTime(started));
        }
        catch (OperationCanceledException ex) when (_stopping.IsCancellationRequested)
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
                _renderSlots.Release();
        }
    }

    private async Task<RenderedArtifact?> FindCachedAsync(RenderKey key)
    {
        try
        {
            return await _store.FindAsync(key, _stopping.Token);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
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
