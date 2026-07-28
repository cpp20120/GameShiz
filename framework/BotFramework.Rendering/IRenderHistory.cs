namespace BotFramework.Rendering;

public interface IRenderHistory
{
    ValueTask RecordAsync(RenderHistoryEntry entry, CancellationToken ct = default);

    IAsyncEnumerable<RenderHistoryEntry> ListAsync(
        string gameId,
        string aggregateId,
        int take = 50,
        CancellationToken ct = default);
}
