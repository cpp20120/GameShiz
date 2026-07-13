using Xunit;

namespace CasinoShiz.Tests;
[Collection("MiniGameSession")]
public class DartsMultiplierTests
{
    public DartsMultiplierTests()
    {
        BotMiniGameSession.DangerousResetAllForTests();
        DartsDiceRoundBinding.DangerousResetAllForTests();
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(6, 2)]
    public void Multipliers_CorrectByFace(int face, int expected)
    {
        Assert.Equal(expected, DartsService.Multipliers[face]);
    }
}
