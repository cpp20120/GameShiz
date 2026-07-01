namespace BotFramework.Host.Admin;

public sealed record CurrentEconomySnapshot(
    DateTimeOffset CapturedAt,
    long WalletSupply,
    long PendingStake,
    int PendingBets,
    int Wallets,
    int FundedWallets,
    long MedianBalance,
    long RichestBalance,
    long Credits24H,
    long Debits24H,
    long Net24H,
    int ActiveUsers24H)
{
    public long TrackedCoins => WalletSupply + PendingStake;
}
