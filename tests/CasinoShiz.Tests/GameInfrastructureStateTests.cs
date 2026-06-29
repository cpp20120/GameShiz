using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class GameInfrastructureStateTests
{
    [Fact]
    public void RedeemTimeouts_ScheduleThenCancel_CancelsAndRemovesToken()
    {
        var timeouts = new RedeemCaptchaTimeouts(NullLogger<RedeemCaptchaTimeouts>.Instance);
        var code = Guid.NewGuid();
        using var cts = timeouts.Schedule(code, default);

        Assert.True(timeouts.TryCancel(code));
        Assert.True(cts.IsCancellationRequested);
        Assert.False(timeouts.TryCancel(code));
    }

    [Fact]
    public void RedeemTimeouts_Reschedule_CancelsPreviousOwner()
    {
        var timeouts = new RedeemCaptchaTimeouts(NullLogger<RedeemCaptchaTimeouts>.Instance);
        var code = Guid.NewGuid();
        using var first = timeouts.Schedule(code, default);
        using var second = timeouts.Schedule(code, default);

        Assert.True(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
        Assert.False(timeouts.Forget(code, first));
        Assert.True(timeouts.Forget(code, second));
    }

    [Fact]
    public void RedeemTimeouts_ApplicationStopping_CancelsLinkedToken()
    {
        var timeouts = new RedeemCaptchaTimeouts(NullLogger<RedeemCaptchaTimeouts>.Instance);
        using var stopping = new CancellationTokenSource();
        using var scheduled = timeouts.Schedule(Guid.NewGuid(), stopping.Token);

        stopping.Cancel();

        Assert.True(scheduled.IsCancellationRequested);
    }

    [Fact]
    public async Task PixelBattleBroadcaster_DeliversToEverySubscriber()
    {
        var broadcaster = new PixelBattleBroadcaster();
        using var first = broadcaster.Subscribe();
        using var second = broadcaster.Subscribe();
        var update = new PixelBattleUpdate(12, "#abcdef", "v1");

        broadcaster.Broadcast(update);

        Assert.Equal(update, await first.Reader.ReadAsync());
        Assert.Equal(update, await second.Reader.ReadAsync());
    }

    [Fact]
    public async Task PixelBattleSubscription_Dispose_CompletesOnlyThatSubscription()
    {
        var broadcaster = new PixelBattleBroadcaster();
        var removed = broadcaster.Subscribe();
        using var active = broadcaster.Subscribe();

        removed.Dispose();
        broadcaster.Broadcast(new PixelBattleUpdate(1, "red", "v2"));

        Assert.False(await removed.Reader.WaitToReadAsync());
        Assert.Equal(1, (await active.Reader.ReadAsync()).Index);
    }

    [Fact]
    public void SpeedGenerator_GenPlaces_AlwaysReturnsValidIndex()
    {
        var places = Enumerable.Range(0, 100).Select(_ => SpeedGenerator.GenPlaces(6)).ToArray();

        Assert.All(places, place => Assert.InRange(place, 0, 5));
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(4, 2)]
    [InlineData(6, 5)]
    public void SpeedGenerator_CreateSpeeds_AssignsShortestSeriesToWinner(int horseCount, int winner)
    {
        var speeds = SpeedGenerator.CreateSpeeds(horseCount, winner);

        Assert.Equal(horseCount, speeds.Length);
        Assert.Equal(speeds.Min(series => series.Length), speeds[winner].Length);
        Assert.All(speeds.SelectMany(series => series), speed => Assert.InRange(speed, 0.21, 0.39));
    }

    [Fact]
    public void HorseResultHelpers_ReturnConsistentFailureShapes()
    {
        var bet = HorseResultHelpers.BetFail(HorseError.InvalidHorseId, horseId: 3, coins: 50);
        var race = HorseResultHelpers.RaceFail(HorseError.NotEnoughBets);

        Assert.Equal(HorseError.InvalidHorseId, bet.Error);
        Assert.Equal(3, bet.HorseId);
        Assert.Equal(50, bet.RemainingCoins);
        Assert.Equal(HorseError.NotEnoughBets, race.Error);
        Assert.Equal(-1, race.Winner);
        Assert.Empty(race.Participants);
    }
}
