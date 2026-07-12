using BotFramework.Sdk.Execution;
using Games.Basketball.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;
public class BasketballMultiplierTests
{
    public BasketballMultiplierTests() => BotMiniGameSession.DangerousResetAllForTests();

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    public void Multipliers_CorrectByFace(int face, int expected)
    {
        Assert.Equal(expected, BasketballService.Multipliers[face]);
    }

    [Fact]
    public void PlaceBet_NegativeAmount_ReturnsFail()
    {
        var result = Place(amount: -5).Result;
        Assert.Equal(BasketballBetError.InvalidAmount, result.Error);
    }

    [Fact]
    public void Throw_WithBetFace5_CreditsX2()
    {
        var decision = Throw(face: 5, amount: 100);
        Assert.Equal(BasketballThrowOutcome.Thrown, decision.Result.Outcome);
        Assert.Equal(2, decision.Result.Multiplier);
        Assert.Equal(200, decision.Result.Payout);
        Assert.Equal([EconomyEffect.Credit(200, "basketball.payout")], decision.Economy);
    }

    [Fact]
    public void Throw_WithBetFace2_ZeroPayout()
    {
        var decision = Throw(face: 2, amount: 50);
        Assert.Equal(0, decision.Result.Payout);
        Assert.Empty(decision.Economy);
    }

    private static GameDecision<BasketballBetState, BasketballBetResult> Place(int amount) =>
        new BasketballPlaceBetAction().Decide(new GameActionInput<BasketballBetState, BasketballPlaceBetCommand>(
            new BasketballPlaceBetCommand(1, "u", 100, amount, "command", 10_000, null),
            new BasketballBetState(null),
            new WalletSnapshot(1_000),
            new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
            {
                [BasketballPlaceBetAction.DailyRollQuota] = new(0, 10),
            },
            new EntropyValue(new Dictionary<string, double>()),
            DateTimeOffset.UnixEpoch));

    private static GameDecision<BasketballBetState, BasketballThrowResult> Throw(int face, int amount) =>
        new BasketballThrowAction().Decide(new GameActionInput<BasketballBetState, BasketballThrowCommand>(
            new BasketballThrowCommand(1, "u", 100, face, "command", 0),
            new BasketballBetState(new BasketballPendingBet(1, 100, amount, DateTimeOffset.UnixEpoch)),
            new WalletSnapshot(900),
            new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
            {
                [BasketballPlaceBetAction.DailyRollQuota] = new(1, 10),
            },
            new EntropyValue(new Dictionary<string, double>
            {
                [BasketballThrowAction.RedeemDropEntropy] = 0.5,
            }),
            DateTimeOffset.UnixEpoch));
}
