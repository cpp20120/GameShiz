namespace BotFramework.Host.Contracts.Economics;

public sealed record WalletEngagement(long Wallets, long Users, long BalanceScopes, long NewWallets24H,
    long NewWallets7D, long ActiveWallets24H, long DailyClaimersToday, long TransactingUsers24H, long ActiveScopes24H);
