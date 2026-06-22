namespace Games.Meta;

public interface IRiskStore
{
    Task UpsertOpenAsync(MetaSeason season, long chatId, long userId, string displayName, string kind, string severity, string reason, string evidenceJson, CancellationToken ct);
    Task<IReadOnlyList<RiskFlagView>> GetOpenAsync(MetaSeason season, long chatId, int limit, CancellationToken ct);
    Task<RiskResolveResult> UpdateStatusAsync(long flagId, string status, CancellationToken ct);
}
