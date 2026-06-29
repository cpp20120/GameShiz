using Xunit;

namespace CasinoShiz.Tests;

public sealed class TransferServiceTests
{
    [Theory]
    [InlineData(1, 1, 10, TransferError.SameUser)]
    [InlineData(1, 2, 4, TransferError.NetBelowMinimum)]
    [InlineData(1, 2, 101, TransferError.NetAboveMaximum)]
    public async Task TryTransferAsync_InvalidRequest_DoesNotTouchEconomics(
        long fromUserId, long toUserId, int amount, TransferError expected)
    {
        var economics = new FakeEconomicsService();
        var service = MakeService(economics, startingMin: 5, max: 100);

        var result = await service.TryTransferAsync(fromUserId, toUserId, 10, "a", "b", amount, default);

        Assert.Equal(expected, result.Error);
        Assert.Empty(economics.Debits);
        Assert.Empty(economics.Credits);
    }

    [Fact]
    public async Task TryTransferAsync_InsufficientFunds_ReturnsCurrentBalance()
    {
        var economics = new FakeEconomicsService { StartingBalance = 10 };

        var result = await MakeService(economics).TryTransferAsync(1, 2, 10, "a", "b", 10, default);

        Assert.Equal(TransferError.InsufficientFunds, result.Error);
        Assert.Equal(10, result.SenderBalance);
        Assert.Empty(economics.Credits);
    }

    [Theory]
    [InlineData(PeerTransferFailure.SameUser, TransferError.SameUser)]
    [InlineData(PeerTransferFailure.SenderMissing, TransferError.SenderMissing)]
    [InlineData(PeerTransferFailure.RecipientMissing, TransferError.RecipientMissing)]
    [InlineData(PeerTransferFailure.InsufficientFunds, TransferError.InsufficientFunds)]
    public async Task TryTransferAsync_MapsEconomicsFailure(
        PeerTransferFailure failure, TransferError expected)
    {
        var economics = new FakeEconomicsService
        {
            StartingBalance = 500,
            ForcedPeerTransferFailure = failure,
        };

        var result = await MakeService(economics).TryTransferAsync(1, 2, 10, "a", "b", 100, default);

        Assert.Equal(expected, result.Error);
        Assert.Equal(500, result.SenderBalance);
    }

    [Fact]
    public async Task TryTransferAsync_Success_TracksStructuredAnalytics()
    {
        var analytics = new RecordingAnalyticsService();
        var economics = new FakeEconomicsService { StartingBalance = 500 };
        var service = new TransferService(
            economics,
            analytics,
            new FakeRuntimeTuning { Transfer = new TransferOptions() });

        await service.TryTransferAsync(1, 2, 10, "a", "b", 100, sourceMessageId: 77, default);

        var tracked = Assert.Single(analytics.Events);
        Assert.Equal(("transfer", "completed"), (tracked.ModuleId, tracked.EventName));
        Assert.Equal(1L, tracked.Tags["from_user_id"]);
        Assert.Equal(2L, tracked.Tags["to_user_id"]);
        Assert.Equal(100, tracked.Tags["net"]);
    }

    [Theory]
    [InlineData(100, 3, 103)]
    [InlineData(1, 1, 2)]
    [InlineData(200, 6, 206)]
    [InlineData(33, 1, 34)]
    [InlineData(42, 2, 44)]
    public void Fee_RoundsPercentToHalfThenWhole_WithMinOne(int net, int expectedFee, int expectedTotal)
    {
        const int minFee = 1;
        var fee = TransferOptions.ComputeFeeCoins(net, 0.03, minFee);
        var total = TransferOptions.ComputeTotalDebit(net, 0.03, minFee);
        Assert.Equal(expectedFee, fee);
        Assert.Equal(expectedTotal, total);
    }

    [Fact]
    public async Task TryTransferAsync_CreditsRecipientNetAndBurnsFee()
    {
        var economics = new FakeEconomicsService { StartingBalance = 500 };
        var svc = new TransferService(
            economics,
            new NullAnalyticsService(),
            new FakeRuntimeTuning { Transfer = new TransferOptions { FeePercent = 0.03, MinFeeCoins = 1 } });

        var r = await svc.TryTransferAsync(1, 2, chatId: -100, "a", "b", netToRecipient: 100, default);

        Assert.Equal(TransferError.None, r.Error);
        Assert.Equal(100, r.NetToRecipient);
        Assert.Equal(3, r.FeeCoins);
        Assert.Equal(103, r.TotalDebited);
        Assert.Equal(397, r.SenderBalance);
        Assert.Equal(600, r.RecipientBalance);

        Assert.Single(economics.Debits);
        Assert.Equal(103, economics.Debits[0].Amount);
        Assert.Single(economics.Credits);
        Assert.Equal(100, economics.Credits[0].Amount);
    }

    private static TransferService MakeService(
        FakeEconomicsService economics,
        int startingMin = 1,
        int max = 0) => new(
            economics,
            new NullAnalyticsService(),
            new FakeRuntimeTuning
            {
                Transfer = new TransferOptions
                {
                    FeePercent = 0.03,
                    MinFeeCoins = 1,
                    MinNetCoins = startingMin,
                    MaxNetCoins = max,
                },
            });
}
