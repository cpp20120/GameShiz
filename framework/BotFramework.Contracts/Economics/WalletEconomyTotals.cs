namespace BotFramework.Host.Contracts.Economics;

public sealed record WalletEconomyTotals(long CoinSupply, long Wallets, long NearZeroWallets,
    long P50Coins, long P90Coins, long P99Coins, long Top1Coins, long Top5Coins, long Top10Coins);
