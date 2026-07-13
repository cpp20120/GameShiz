using BotFramework.Sdk.Execution;
using Games.Horse.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class HorseServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Coefficients_EvenSplit_UseLegacyFormula()
    {
        var result = HorseRules.GetCoefficients(new Dictionary<int, int> { [0] = 100, [1] = 100 });
        Assert.Equal(1.909, result[0], 3);
        Assert.Equal(1.909, result[1], 3);
    }

    [Fact]
    public void PlaceBet_InvalidHorse_IsRejectedWithoutDebit()
    {
        var decision = new HorsePlaceBetAction().Decide(BetInput(horseId: 5, amount: 50, balance: 100));
        Assert.Equal(HorseError.InvalidHorseId, decision.Result.Error);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void PlaceBet_AcceptsStateDebitAndEventTogether()
    {
        var decision = new HorsePlaceBetAction().Decide(BetInput(horseId: 2, amount: 50, balance: 200));
        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(150, decision.Result.RemainingCoins);
        Assert.Equal(1, decision.NewState.Bet?.HorseId);
        Assert.Equal(50, Assert.Single(decision.Economy).Amount);
        Assert.IsType<HorseBetPlaced>(Assert.Single(decision.Events));
    }

    [Fact]
    public void RunRace_NotAdmin_IsRejectedWithoutEffects()
    {
        var decision = RaceDecision([Bet(1, 10, 0, 50)], isAdmin: false, horseCount: 1);
        Assert.Equal(HorseError.NotAdmin, decision.Result.Error);
        Assert.Null(decision.CustomEffects);
    }

    [Fact]
    public void RunRace_UsesFrameworkEntropyAndBuildsMultiWalletPayouts()
    {
        HorseBetRow[] bets = [Bet(1, 10, 0, 100), Bet(2, 20, 1, 100)];
        var decision = RaceDecision(bets, isAdmin: true, horseCount: 2, entropy: 0.75);
        Assert.Equal(1, decision.Result.Winner);
        var payout = Assert.IsType<WalletEconomyEffect>(Assert.Single(decision.CustomEffects!));
        Assert.Equal(2, payout.UserId);
        Assert.Equal(190, payout.Amount);
        Assert.IsType<HorseRaceFinished>(Assert.Single(decision.Events));
    }

    [Fact]
    public void RunRace_SingleHorse_ReturnsStakeAndAllResultScopes()
    {
        HorseBetRow[] bets = [Bet(1, 10, 0, 100), Bet(2, 20, 0, 50)];
        var decision = RaceDecision(bets, isAdmin: true, horseCount: 1);
        Assert.Equal(2, decision.CustomEffects!.Count);
        Assert.Equal(new long[] { 0, 10, 20 }, decision.NewState.ResultScopes.Order());
        Assert.Equal(2, decision.Result.Participants.Count);
    }

    private static GameActionInput<HorseBetState, HorsePlaceBetCommand> BetInput(
        int horseId, int amount, long balance)
    {
        var command = new HorsePlaceBetCommand(1, "u", 10, horseId, amount, "07-13-2026",
            Guid.NewGuid(), "bet", 4);
        return new(command, new(null), new(balance), new Dictionary<string, QuotaSnapshot>(),
            new(new Dictionary<string, double>()), Now);
    }

    private static GameDecision<HorseRaceState, RaceOutcome> RaceDecision(
        IReadOnlyList<HorseBetRow> bets, bool isAdmin, int horseCount, double entropy = 0)
    {
        var command = new HorseRunCommand(99, HorseRunKind.Global, 0, 0, "07-13-2026",
            bets, "run", horseCount, 1, isAdmin);
        var input = new GameActionInput<HorseRaceState, HorseRunCommand>(command,
            new(bets, [], null), new(0), new Dictionary<string, QuotaSnapshot>(),
            new(new Dictionary<string, double> { [HorseRunAction.WinnerEntropy] = entropy }), Now);
        return new HorseRunAction().Decide(input);
    }

    private static HorseBetRow Bet(long userId, long scopeId, int horseId, int amount) =>
        new(Guid.NewGuid(), "07-13-2026", userId, scopeId, horseId, amount);
}
