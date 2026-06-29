using BotFramework.Host.Contracts.ResponsibleGaming;
using BotFramework.Host.Economics.Services;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PlayerProtectionTests
{
    [Theory]
    [InlineData("dice.stake")]
    [InlineData("blackjack.start")]
    [InlineData("poker.join")]
    [InlineData("challenge.create")]
    [InlineData("pick.daily.buy")]
    public void GameDebits_AreProtected(string reason) =>
        Assert.True(EconomicsService.IsProtectedWager(reason));

    [Theory]
    [InlineData("admin.pay")]
    [InlineData("transfer.send")]
    [InlineData("pick.lottery.win.rollback")]
    public void OperationalDebits_BypassProtection(string reason) =>
        Assert.False(EconomicsService.IsProtectedWager(reason));

    [Fact]
    public void ProtectionException_PreservesActionableDetails()
    {
        var until = DateTimeOffset.UtcNow.AddDays(7);
        var exception = new PlayerProtectionException(
            "daily_limit", until, dailyLimit: 500, usedToday: 480);

        Assert.Equal("daily_limit", exception.ReasonCode);
        Assert.Equal(until, exception.BlockedUntil);
        Assert.Equal(500, exception.DailyLimit);
        Assert.Equal(480, exception.UsedToday);
    }
}
