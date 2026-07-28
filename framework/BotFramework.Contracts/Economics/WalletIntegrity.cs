namespace BotFramework.Host.Contracts.Economics;

public sealed record WalletIntegrity(long WalletCoinSupply, long LatestLedgerSupply, long MismatchedWallets,
    long MismatchAbsoluteCoins, long WalletsWithoutLedger, double BalanceGini, double TopDecileCoinSharePercent);
