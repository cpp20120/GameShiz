namespace BotFramework.Host.Contracts.Economics;

public sealed record LedgerGameVolume(string Module, long Rows, long Stake, long Payout, long Net, long Users);
