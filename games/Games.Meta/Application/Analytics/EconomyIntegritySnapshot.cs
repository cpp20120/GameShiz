namespace Games.Meta.Application.Analytics;

internal sealed record EconomyIntegritySnapshot(
    long WalletCoinSupply,
    long LatestLedgerSupply,
    long MismatchedWallets,
    long MismatchAbsoluteCoins,
    long WalletsWithoutLedger,
    double BalanceGini,
    double TopDecileCoinSharePercent);
