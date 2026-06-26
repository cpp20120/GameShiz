using Games.PixelBattle.Domain.Configuration;
using Games.PixelBattle.Domain.Models;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PixelBattleDomainTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(31_999, true)]
    [InlineData(-1, false)]
    [InlineData(32_000, false)]
    public void IsValidIndex_RespectsGridBounds(int index, bool expected) =>
        Assert.Equal(expected, PixelBattleConstants.IsValidIndex(index));

    [Theory]
    [InlineData("#FFFFFF", true)]
    [InlineData("#ffffff", true)]
    [InlineData("#000000", true)]
    [InlineData("#123456", false)]
    [InlineData("", false)]
    public void IsValidColor_AcceptsPaletteCaseInsensitively(string color, bool expected) =>
        Assert.Equal(expected, PixelBattleConstants.IsValidColor(color));

    [Fact]
    public void FullUpdateInterval_ClampsToAtLeastOneSecond()
    {
        var options = new PixelBattleOptions { FullUpdateIntervalMs = 5 };
        Assert.Equal(TimeSpan.FromSeconds(1), options.FullUpdateInterval);
    }

    [Fact]
    public void MaxInitDataAge_ClampsToAtLeastOneMinute()
    {
        var options = new PixelBattleOptions { MaxInitDataAgeSeconds = 5 };
        Assert.Equal(TimeSpan.FromMinutes(1), options.MaxInitDataAge);
    }
}
