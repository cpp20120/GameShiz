using System.Text.Json;
using BotFramework.Sdk.Execution;
using Games.Blackjack.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class BlackjackActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Start_InvalidBetRejectsWithoutEffects(int bet)
    {
        var decision = Start(bet: bet, balance: 100);

        Assert.Equal(BlackjackError.InvalidBet, decision.Result.Error);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Schedules);
    }

    [Fact]
    public void Start_InsufficientBalanceRejectsWithoutCreatingHand()
    {
        var decision = Start(bet: 50, balance: 49);

        Assert.Equal(BlackjackError.NotEnoughCoins, decision.Result.Error);
        Assert.Null(decision.NewState.Hand);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void Start_SameEntropyProducesBitwiseSameDecision()
    {
        var first = Start(entropyValue: 0.37);
        var second = Start(entropyValue: 0.37);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Start_NonNaturalHandDebitsAndSchedulesAtomicTimeout()
    {
        var decision = FindActiveStart();

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(TurnGameStatus.Active, decision.NewState.Status);
        Assert.NotNull(decision.NewState.Hand);
        Assert.Single(decision.Economy);
        Assert.Equal(EconomyEffectKind.Debit, decision.Economy[0].Kind);
        var schedule = Assert.Single(decision.Schedules);
        Assert.Equal(ScheduleEffectKind.Schedule, schedule.Kind);
        Assert.Equal(AtomicGameSchedule.JobKey<BlackjackTimeoutCommand>(), schedule.JobKey);
    }

    [Fact]
    public void Hit_AdvancesRevisionAndConsumesStoredDeck()
    {
        var state = ActiveState(["5S", "5H"], "2S 3S 4S");
        var decision = Turn(state, BlackjackTurnKind.Hit);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(5, decision.NewState.Revision);
        Assert.Equal(["5S", "5H", "2S"], decision.NewState.Hand!.PlayerCards);
        Assert.Equal("3S 4S", decision.NewState.Hand.DeckState);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void Double_DebitPayoutStateAndTimeoutCancelAreOneDecision()
    {
        var state = ActiveState(["TS", "9H"], "2S 3S 4S");
        var decision = Turn(state, BlackjackTurnKind.DoubleDown, balance: 100);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(TurnGameStatus.Completed, decision.NewState.Status);
        Assert.Null(decision.NewState.Hand);
        Assert.Contains(decision.Economy, effect => effect.Kind == EconomyEffectKind.Debit);
        Assert.Equal(ScheduleEffectKind.Cancel, Assert.Single(decision.Schedules).Kind);
        Assert.NotNull(decision.Result.Snapshot?.Outcome);
    }

    [Fact]
    public void Turn_StaleRevisionRejectedBeforeRules()
    {
        var state = ActiveState(["5S", "5H"], "2S");
        var command = new BlackjackTurnCommand(1, "player", 10, BlackjackTurnKind.Hit, 3, "hit-stale");
        var decision = new BlackjackTurnAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal("stale_revision", decision.RejectionReason);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void Timeout_SettlesCurrentHandAndCancelsSchedule()
    {
        var state = ActiveState(["TS", "8H"], "2S 3S");
        var command = new BlackjackTimeoutCommand(1, "player", 10, "hand-1", "timeout-1");
        var decision = new BlackjackTimeoutAction().Decide(Input(command, state, utcNow: Now.AddMinutes(2)));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(TurnGameStatus.Completed, decision.NewState.Status);
        Assert.Equal(ScheduleEffectKind.Cancel, Assert.Single(decision.Schedules).Kind);
    }

    private static GameDecision<BlackjackGameState, BlackjackResult> Start(
        int bet = 10,
        long balance = 100,
        double entropyValue = 0.5)
    {
        var state = new BlackjackGameState(0, TurnGameStatus.Completed, 1, null, "player", null);
        var command = new BlackjackStartCommand(1, "player", 10, bet, "start-1", 1, 100, 60_000);
        return new BlackjackStartAction().Decide(Input(command, state, balance, entropyValue));
    }

    private static GameDecision<BlackjackGameState, BlackjackResult> FindActiveStart()
    {
        for (var index = 1; index < 99; index++)
        {
            var decision = Start(entropyValue: index / 100d);
            if (decision.NewState.Status == TurnGameStatus.Active)
                return decision;
        }
        throw new InvalidOperationException("Could not build a non-natural deterministic hand.");
    }

    private static GameDecision<BlackjackGameState, BlackjackResult> Turn(
        BlackjackGameState state,
        BlackjackTurnKind kind,
        long balance = 100)
    {
        var command = new BlackjackTurnCommand(1, "player", 10, kind, state.Revision, $"turn-{kind}");
        return new BlackjackTurnAction().Decide(Input(command, state, balance));
    }

    private static BlackjackGameState ActiveState(string[] playerCards, string deck) =>
        new(
            4,
            TurnGameStatus.Active,
            1,
            Now.AddMinutes(1),
            "player",
            new BlackjackHandState(
                "hand-1",
                1,
                10,
                10,
                playerCards,
                ["9S", "7H"],
                deck,
                20,
                Now));

    private static GameActionInput<BlackjackGameState, TCommand> Input<TCommand>(
        TCommand command,
        BlackjackGameState state,
        long balance = 100,
        double entropyValue = 0.5,
        DateTimeOffset? utcNow = null) =>
        new(
            command,
            state,
            new WalletSnapshot(balance),
            new Dictionary<string, QuotaSnapshot>(),
            new EntropyValue(
                Enumerable.Range(1, 51)
                    .Select(index => KeyValuePair.Create($"shuffle-{index}", entropyValue))),
            utcNow ?? Now);
}
