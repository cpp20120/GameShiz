using Games.Pick.Application.Services;
using Games.Pick.Domain.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PickDailyLotteryTimeTests
{
    [Fact]
    public void OffsetHours_UsesDiceLimitWhenDailyOverrideIsZero()
    {
        var service = MakeService(offsetOverride: 0, diceOffset: 7, drawHour: 18);

        Assert.Equal(7, service.OffsetHours);
    }

    [Fact]
    public void OffsetHours_DailyOverrideTakesPrecedence()
    {
        var service = MakeService(offsetOverride: -4, diceOffset: 7, drawHour: 18);

        Assert.Equal(-4, service.OffsetHours);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(18, 18)]
    [InlineData(99, 23)]
    public void DrawHourLocal_ClampsToValidClockHour(int configured, int expected)
    {
        Assert.Equal(expected, MakeService(0, 0, configured).DrawHourLocal);
    }

    [Fact]
    public void LocalNextDrawUtc_IsUpcomingAndMatchesLocalToday()
    {
        var service = MakeService(offsetOverride: 7, diceOffset: 0, drawHour: 18);
        var before = DateTime.UtcNow;

        var drawUtc = service.LocalNextDrawUtc();
        var drawLocal = new DateTimeOffset(drawUtc, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(7));

        Assert.InRange(drawUtc, before.AddSeconds(-1), before.AddHours(24));
        Assert.Equal(service.LocalToday(), DateOnly.FromDateTime(drawLocal.Date));
        Assert.Equal(18, drawLocal.Hour);
    }

    private static PickDailyLotteryService MakeService(int offsetOverride, int diceOffset, int drawHour) => new(
        null!,
        null!,
        null!,
        Options.Create(new PickOptions
        {
            Daily = new PickDailyLotteryOptions
            {
                TimezoneOffsetHoursOverride = offsetOverride,
                DrawHourLocal = drawHour,
            },
        }),
        Options.Create(new TelegramDiceDailyLimitOptions { TimezoneOffsetHours = diceOffset }));
}
