namespace BotFramework.Rendering;

public interface IRenderArtifactStore
{
    ValueTask<RenderedArtifact?> FindAsync(RenderKey key, CancellationToken ct);

    ValueTask<RenderedArtifact> PutAsync(RenderKey key, RenderOutput output, CancellationToken ct);

    ValueTask RecordHistoryAsync(RenderHistoryEntry entry, CancellationToken ct);

    IAsyncEnumerable<RenderHistoryEntry> ListHistoryAsync(
        string gameId,
        string aggregateId,
        int take,
        CancellationToken ct);
}
