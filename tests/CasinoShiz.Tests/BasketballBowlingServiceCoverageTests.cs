using Xunit;

namespace CasinoShiz.Tests;

[Collection("MiniGameSession")]
public sealed class BasketballBowlingServiceCoverageTests
{
    public BasketballBowlingServiceCoverageTests() => BotMiniGameSession.DangerousResetAllForTests();

    private static BowlingService Bowling(
        FakeEconomicsService? economics = null,
        InMemoryBowlingBetStore? bets = null,
        IAnalyticsService? analytics = null,
        IDomainEventBus? events = null,
        ITelegramDiceDailyRollLimiter? rolls = null) =>
        new(economics ?? new FakeEconomicsService(), analytics ?? new NullAnalyticsService(),
            bets ?? new InMemoryBowlingBetStore(), events ?? new NullEventBus(),
            new FakeRuntimeTuning(), new NullMiniGameSessionGhostHeal(),
            rolls ?? new NullTelegramDiceDailyRollLimiter());

    [Fact]
    public async Task Bowling_RejectsInsufficientBalance()
    {
        var economics = new FakeEconomicsService { StartingBalance = 5 };
        var result = await Bowling(economics).PlaceBetAsync(1, "u", 10, 6, default);
        Assert.Equal(BowlingBetError.NotEnoughCoins, result.Error);
        Assert.Equal(5, result.Balance);
    }

    [Fact]
    public async Task Bowling_RejectsDailyLimit()
    {
        var result = await Bowling(rolls: new RejectingTelegramDiceDailyRollLimiter())
            .PlaceBetAsync(1, "u", 10, 5, default);
        Assert.Equal(BowlingBetError.DailyRollLimit, result.Error);
        Assert.Equal(3, result.DailyRollUsed);
        Assert.Equal(10, result.DailyRollLimit);
    }

    [Fact]
    public async Task Bowling_RollWithoutBet_ReturnsNoBet()
    {
        var result = await Bowling().RollAsync(1, "u", 10, 6, default);
        Assert.Equal(BowlingRollOutcome.NoBet, result.Outcome);
    }

    [Fact]
    public async Task Bowling_RollPublishesEventsAndAnalytics()
    {
        var events = new NullEventBus();
        var analytics = new RecordingAnalyticsService();
        var service = Bowling(events: events, analytics: analytics);
        await service.PlaceBetAsync(1, "u", 10, 25, default);
        var result = await service.RollAsync(1, "u", 10, 6, default);

        Assert.Equal(50, result.Payout);
        Assert.Single(events.Published.OfType<BowlingRollCompleted>());
        Assert.Single(events.Published.OfType<GameCompletedMetaEvent>());
        Assert.Contains(analytics.Events, x => x.ModuleId == "bowling" && x.EventName == "bet");
        Assert.Contains(analytics.Events, x => x.ModuleId == "bowling" && x.EventName == "roll");
    }

    [Fact]
    public async Task Bowling_AbortRefundsAndRemovesBet()
    {
        var economics = new FakeEconomicsService();
        var bets = new InMemoryBowlingBetStore();
        var analytics = new RecordingAnalyticsService();
        var service = Bowling(economics, bets, analytics);
        await service.PlaceBetAsync(1, "u", 10, 30, default);
        await service.AbortPendingBetAfterSendDiceFailedAsync(1, 10, default);

        Assert.Contains(economics.Credits, x => x.Amount == 30 && x.Reason == "bowling.send_dice_failed");
        Assert.Null(await bets.FindAsync(1, 10, default));
        Assert.Contains(analytics.Events, x => x.EventName == "bet_aborted");
    }
}
