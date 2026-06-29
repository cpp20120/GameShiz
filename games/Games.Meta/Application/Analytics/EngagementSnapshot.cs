namespace Games.Meta.Application.Analytics;

internal sealed record EngagementSnapshot(
    long Wallets,
    long Users,
    long BalanceScopes,
    long NewWallets24H,
    long NewWallets7D,
    long ActiveWallets24H,
    long DailyClaimersToday,
    long TransactingUsers24H,
    long ActiveScopes24H,
    long ActiveChats24H);
