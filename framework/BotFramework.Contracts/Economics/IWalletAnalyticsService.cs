namespace BotFramework.Host.Contracts.Economics;

public interface IWalletAnalyticsService
{
    Task<long> CountCreatedAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<WalletEconomyTotals> GetTotalsAsync(CancellationToken ct);
    Task<IReadOnlyList<WalletWhale>> ListWhalesAsync(int limit, CancellationToken ct);
    Task<WalletEngagement> GetEngagementAsync(CancellationToken ct);
    Task<WalletIntegrity> GetIntegrityAsync(CancellationToken ct);
    Task<WalletHealth> GetHealthAsync(CancellationToken ct);
    Task<IReadOnlyList<LedgerReasonVolume>> ListReasonVolumesAsync(int windowMinutes, CancellationToken ct);
    Task<IReadOnlyList<LedgerGameVolume>> ListGameVolumesAsync(int windowMinutes, CancellationToken ct);
    Task<LedgerHealth> GetLedgerHealthAsync(int windowMinutes, CancellationToken ct);
    Task<WalletPeriodSummary> GetPeriodSummaryAsync(DateTimeOffset from, DateTimeOffset to, int topGames, CancellationToken ct);
    Task<WalletMutationHealth> GetMutationHealthAsync(int windowMinutes, int hugeThreshold, CancellationToken ct);
    Task<WalletSocialActivity> GetSocialActivityAsync(DateTimeOffset from, CancellationToken ct);
    Task<IReadOnlyList<WalletLedgerEntry>> ListLedgerAsync(long? userId, long? balanceScopeId,
        int limit, CancellationToken ct);
}
