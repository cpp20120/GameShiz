namespace Games.Meta;

public interface IMetaHistoryStore
{
    Task AppendAsync(string eventType, string aggregateType, string aggregateId, long? seasonId, long? chatId, long? userId, object payload, CancellationToken ct);
    Task<IReadOnlyList<MetaHistoryEvent>> ListAsync(string? eventType, string? aggregateType, string? aggregateId, long? chatId, long? userId, int limit, CancellationToken ct);
    Task<MetaHistoryStats> GetStatsAsync(CancellationToken ct);
}
