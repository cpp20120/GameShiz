using Xunit;

namespace CasinoShiz.Tests;

[Collection("MiniGameSession")]
public class FootballServiceTests
{
    public FootballServiceTests() => BotMiniGameSession.DangerousResetAllForTests();

    private static FootballService MakeService(
        FakeEconomicsService? economics = null,
        InMemoryFootballBetStore? bets = null,
        IMiniGameSessionGhostHeal? ghostHeal = null) =>
        new(
            economics ?? new FakeEconomicsService(),
            new NullAnalyticsService(),
            bets ?? new InMemoryFootballBetStore(),
            new NullEventBus(),
            new FakeRuntimeTuning(),
            ghostHeal ?? new NullMiniGameSessionGhostHeal(),
            new NullTelegramDiceDailyRollLimiter());

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    public async Task ThrowAsync_ReturnsCorrectMultiplier(int face, int expectedMultiplier)
    {
        var bets = new InMemoryFootballBetStore();
        var svc = MakeService(bets: bets);
        await svc.PlaceBetAsync(1, "u", 100, 50, default);
        var result = await svc.ThrowAsync(1, "u", 100, face, default);
        Assert.Equal(expectedMultiplier, result.Multiplier);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 100)]
    [InlineData(5, 100)]
    public async Task ThrowAsync_ReturnsCorrectPayout(int face, int expectedPayout)
    {
        var bets = new InMemoryFootballBetStore();
        var svc = MakeService(bets: bets);
        await svc.PlaceBetAsync(1, "u", 100, 50, default);
        var result = await svc.ThrowAsync(1, "u", 100, face, default);
        Assert.Equal(expectedPayout, result.Payout);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ThrowAsync_WinningFace_CreditsPayout(int face)
    {
        var econ = new FakeEconomicsService();
        var bets = new InMemoryFootballBetStore();
        var svc = MakeService(economics: econ, bets: bets);
        await svc.PlaceBetAsync(1, "u", 100, 50, default);
        await svc.ThrowAsync(1, "u", 100, face, default);
        Assert.Single(econ.Credits);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ThrowAsync_LosingFace_NoCredit(int face)
    {
        var econ = new FakeEconomicsService();
        var bets = new InMemoryFootballBetStore();
        var svc = MakeService(economics: econ, bets: bets);
        await svc.PlaceBetAsync(1, "u", 100, 50, default);
        await svc.ThrowAsync(1, "u", 100, face, default);
        Assert.Empty(econ.Credits);
    }

    [Fact]
    public async Task ThrowAsync_UnknownFace_NoPayout()
    {
        var bets = new InMemoryFootballBetStore();
        var svc = MakeService(bets: bets);
        await svc.PlaceBetAsync(1, "u", 100, 50, default);
        var result = await svc.ThrowAsync(1, "u", 100, 99, default);
        Assert.Equal(0, result.Payout);
        Assert.Equal(0, result.Multiplier);
    }

    [Fact]
    public async Task ThrowAsync_PublishesThrowCompletedEvent()
    {
        var bus = new NullEventBus();
        var bets = new InMemoryFootballBetStore();
        var svc = new FootballService(new FakeEconomicsService(), new NullAnalyticsService(), bets, bus,
            new FakeRuntimeTuning(), new NullMiniGameSessionGhostHeal(), new NullTelegramDiceDailyRollLimiter());
        await svc.PlaceBetAsync(1, "u", 100, 50, default);
        await svc.ThrowAsync(1, "u", 100, 4, default);
        Assert.Single(bus.Published.OfType<FootballThrowCompleted>());
        Assert.Single(bus.Published.OfType<GameCompletedMetaEvent>());
    }

    [Fact]
    public void Multipliers_ContainsAllFiveFaces()
    {
        for (var face = 1; face <= 5; face++)
            Assert.True(FootballService.Multipliers.ContainsKey(face));
    }

    [Fact]
    public async Task PlaceBetAsync_InsufficientBalance_ReturnsBalance()
    {
        var economics = new FakeEconomicsService { StartingBalance = 5 };
        var result = await MakeService(economics).PlaceBetAsync(1, "u", 100, 6, default);
        Assert.Equal(FootballBetError.NotEnoughCoins, result.Error);
        Assert.Equal(5, result.Balance);
    }

    [Fact]
    public async Task PlaceBetAsync_DailyLimit_ReturnsGateStatus()
    {
        var service = new FootballService(
            new FakeEconomicsService(), new NullAnalyticsService(), new InMemoryFootballBetStore(),
            new NullEventBus(), new FakeRuntimeTuning(), new NullMiniGameSessionGhostHeal(),
            new RejectingTelegramDiceDailyRollLimiter());

        var result = await service.PlaceBetAsync(1, "u", 100, 10, default);
        Assert.Equal(FootballBetError.DailyRollLimit, result.Error);
        Assert.Equal(3, result.DailyRollUsed);
        Assert.Equal(10, result.DailyRollLimit);
    }

    [Fact]
    public async Task ThrowAsync_WithoutBet_ReturnsNoBet()
    {
        var result = await MakeService().ThrowAsync(1, "u", 100, 5, default);
        Assert.Equal(FootballThrowOutcome.NoBet, result.Outcome);
    }

    [Fact]
    public async Task ThrowAsync_TracksStructuredAnalytics()
    {
        var analytics = new RecordingAnalyticsService();
        var bets = new InMemoryFootballBetStore();
        var service = new FootballService(
            new FakeEconomicsService(), analytics, bets, new NullEventBus(), new FakeRuntimeTuning(),
            new NullMiniGameSessionGhostHeal(), new NullTelegramDiceDailyRollLimiter());

        await service.PlaceBetAsync(1, "u", 100, 20, default);
        await service.ThrowAsync(1, "u", 100, 4, default);
        Assert.Contains(analytics.Events, x => x.ModuleId == "football" && x.EventName == "bet");
        Assert.Contains(analytics.Events, x => x.ModuleId == "football" && x.EventName == "throw");
    }

    [Fact]
    public async Task AbortPendingBet_RefundsDeletesAndTracks()
    {
        var economics = new FakeEconomicsService();
        var analytics = new RecordingAnalyticsService();
        var bets = new InMemoryFootballBetStore();
        var rolls = new RecordingTelegramDiceDailyRollLimiter();
        var service = new FootballService(
            economics, analytics, bets, new NullEventBus(), new FakeRuntimeTuning(),
            new NullMiniGameSessionGhostHeal(), rolls);

        await service.PlaceBetAsync(1, "u", 100, 20, default);
        await service.AbortPendingBetAfterSendDiceFailedAsync(1, 100, default);

        Assert.Contains(economics.Credits, x => x.Amount == 20 && x.Reason == "football.send_dice_failed");
        Assert.Null(await bets.FindAsync(1, 100, default));
        Assert.Equal(1, rolls.RefundCount);
        Assert.Contains(analytics.Events, x => x.EventName == "bet_aborted");
    }
}
