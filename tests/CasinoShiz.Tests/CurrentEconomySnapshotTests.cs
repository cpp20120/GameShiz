using BotFramework.Host.Admin;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class CurrentEconomySnapshotTests
{
    [Fact]
    public void TrackedCoins_IncludesWalletSupplyAndUnresolvedStake()
    {
        var snapshot = new CurrentEconomySnapshot(
            DateTimeOffset.UnixEpoch,
            WalletSupply: 10_000,
            PendingStake: 750,
            PendingBets: 3,
            Wallets: 10,
            FundedWallets: 8,
            MedianBalance: 500,
            RichestBalance: 4_000,
            Credits24H: 1_000,
            Debits24H: 800,
            Net24H: 200,
            ActiveUsers24H: 5);

        Assert.Equal(10_750, snapshot.TrackedCoins);
    }
}
