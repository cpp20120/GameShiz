namespace BotFramework.Host.Contracts.Economics;

public sealed record WalletPeriodSummary(long ActiveUsers, long Stake, long Payout, IReadOnlyList<LedgerGameVolume> TopGames);
