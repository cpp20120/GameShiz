namespace Games.Meta;

public interface IRiskService
{
    Task EvaluateGameCompletedAsync(GameCompletedMetaEvent ev, SeasonPlayer player, CancellationToken ct);
    Task<IReadOnlyList<RiskFlagView>> GetOpenAsync(long chatId, int limit, CancellationToken ct);
    Task<RiskResolveResult> UpdateStatusAsync(long flagId, string status, CancellationToken ct);
}
