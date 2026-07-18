namespace BotFramework.Host.Contracts.Economics;

public sealed record WalletEconomyTotals(long CoinSupply, long Wallets, long NearZeroWallets,
    long P50Coins, long P90Coins, long P99Coins, long Top1Coins, long Top5Coins, long Top10Coins);
public sealed record WalletWhale(long UserId, long BalanceScopeId, int Coins, int Rank);
public sealed record WalletEngagement(long Wallets, long Users, long BalanceScopes, long NewWallets24H,
    long NewWallets7D, long ActiveWallets24H, long DailyClaimersToday, long TransactingUsers24H, long ActiveScopes24H);
public sealed record WalletIntegrity(long WalletCoinSupply, long LatestLedgerSupply, long MismatchedWallets,
    long MismatchAbsoluteCoins, long WalletsWithoutLedger, double BalanceGini, double TopDecileCoinSharePercent);
public sealed record WalletHealth(long NegativeWallets, long MismatchedWallets);
public sealed record LedgerReasonVolume(string Reason, long Rows, long Credits, long Debits, long Net);
public sealed record LedgerGameVolume(string Module, long Rows, long Stake, long Payout, long Net, long Users);
public sealed record LedgerHealth(long RowsWindow, long CreditsWindow, long DebitsWindow, long NetWindow,
    long IdempotentRows, long NegativeBalanceRows, long ZeroDeltaRows, DateTimeOffset LastLedgerAt, double LastLedgerAgeSeconds);
public sealed record WalletPeriodSummary(long ActiveUsers, long Stake, long Payout, IReadOnlyList<LedgerGameVolume> TopGames);
public sealed record WalletMutationHealth(long LargestMutation, long HugeMutations);
public sealed record WalletSocialActivity(long Transfers, long TransferCoins, long Users);
public sealed record WalletLedgerEntry(long Id, long UserId, long BalanceScopeId, int Delta,
    int BalanceAfter, string Reason, DateTimeOffset CreatedAt);

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
