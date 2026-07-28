namespace BotFramework.Host.Contracts.Economics;

public sealed record WalletLedgerEntry(long Id, long UserId, long BalanceScopeId, int Delta,
    int BalanceAfter, string Reason, DateTimeOffset CreatedAt);
