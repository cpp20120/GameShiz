using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace BotFramework.Rendering;

internal sealed class InMemoryRenderArtifactStore(TimeProvider timeProvider) : IRenderArtifactStore
{
    private readonly ConcurrentDictionary<string, RenderedArtifact> artifacts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RenderHistoryEntry> history = new(StringComparer.Ordinal);

    public ValueTask<RenderedArtifact?> FindAsync(RenderKey key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(artifacts.TryGetValue(key.Value, out var artifact)
            ? artifact with { CacheHit = true }
            : null);
    }

    public ValueTask<RenderedArtifact> PutAsync(RenderKey key, RenderOutput output, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var artifact = new RenderedArtifact(
            key,
            output.Content,
            output.FileName ?? $"render.{key.Extension.TrimStart('.')}",
            timeProvider.GetUtcNow(),
            key.ObjectName,
            false);
        artifacts[key.Value] = artifact;
        return ValueTask.FromResult(artifact);
    }

    public ValueTask RecordHistoryAsync(RenderHistoryEntry entry, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        history[HistoryKey(entry)] = entry;
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<RenderHistoryEntry> ListHistoryAsync(
        string gameId,
        string aggregateId,
        int take,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var prefix = $"{gameId}/{aggregateId}/";
        foreach (var entry in history
                     .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                     .Select(static pair => pair.Value)
                     .OrderByDescending(static item => item.CreatedAt)
                     .Take(Math.Max(0, take)))
        {
            ct.ThrowIfCancellationRequested();
            yield return entry;
            await Task.Yield();
        }
    }

    private static string HistoryKey(RenderHistoryEntry entry) =>
        $"{entry.GameId}/{entry.AggregateId}/{entry.MatchId}";
}
