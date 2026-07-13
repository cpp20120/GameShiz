using Xunit;

namespace CasinoShiz.Tests;

public sealed class FootballMultiplierTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    public void Multipliers_CorrectByFace(int face, int expected) =>
        Assert.Equal(expected, FootballService.Multipliers[face]);
}
