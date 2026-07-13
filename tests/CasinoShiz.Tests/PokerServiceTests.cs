using BotFramework.Sdk.Execution;
using Games.Poker.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PokerServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateDecision_IsPureDeterministicAndDebitsBuyIn()
    {
        var command = new PokerCreateCommand(1, "alice", 10, "create", 100, 1, 2, [new(1, 10)]);
        var input = Input(command, new(null, [], 500),
            new Dictionary<string, double> { [PokerExecutionRules.InviteEntropy] = 0.5 });

        var first = new PokerCreateAction().Decide(input);
        var second = new PokerCreateAction().Decide(input);

        Assert.Equal(first.Result, second.Result);
        Assert.Equal(DecisionStatus.Accepted, first.Status);
        Assert.Equal(5, first.Result.InviteCode.Length);
        Assert.Equal(100, Assert.IsType<WalletEconomyEffect>(Assert.Single(first.CustomEffects!)).Amount);
        Assert.Single(first.NewState.Seats);
    }

    [Fact]
    public void JoinDecision_UsesFirstFreeSeatAndDoesNotDuplicatePlayer()
    {
        var state = State();
        state.Seats.Add(Seat(2, 2));
        var command = new PokerJoinCommand("ABCDE", 3, "carol", 10, "join", 100, 8, [new(3, 10)]);

        var decision = new PokerJoinAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(3, decision.Result.Seated);
        Assert.Equal(1, decision.NewState.Seats.Single(seat => seat.UserId == 3).Position);
    }

    [Fact]
    public void StartDecision_UsesFrameworkEntropyAndProducesSameDeck()
    {
        var state = State();
        state.Seats.Add(Seat(2, 1));
        var command = new PokerStartCommand("ABCDE", 1, "alice", 10, "start", []);
        var entropy = PokerExecutionRules.ShuffleEntropyNames.ToDictionary(name => name, _ => 0.25);

        var first = new PokerStartAction().Decide(Input(command, state, entropy));
        var second = new PokerStartAction().Decide(Input(command, state, entropy));

        Assert.Equal(DecisionStatus.Accepted, first.Status);
        Assert.Equal(first.NewState.Table!.DeckState, second.NewState.Table!.DeckState);
        Assert.Equal(first.NewState.Seats[0].HoleCards, second.NewState.Seats[0].HoleCards);
    }

    [Fact]
    public void PlayerTurn_RejectsWrongActorWithoutMutatingState()
    {
        var state = State();
        state.Seats.Add(Seat(2, 1));
        state.Table!.Status = PokerTableStatus.HandActive;
        state.Table.CurrentSeat = 0;
        var command = new PokerPlayerTurnCommand("ABCDE", 2, "bob", 10, "turn", "fold", 0, []);

        var decision = new PokerPlayerTurnAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(PokerError.NotYourTurn, decision.Result.Error);
        Assert.Same(state, decision.NewState);
    }

    [Fact]
    public void LeaveDecision_RefundsStackAsWalletEffect()
    {
        var state = State();
        var command = new PokerLeaveCommand("ABCDE", 1, "alice", 10, "leave", [new(1, 10)]);

        var decision = new PokerLeaveAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        var refund = Assert.IsType<WalletEconomyEffect>(Assert.Single(decision.CustomEffects!));
        Assert.Equal(100, refund.Amount);
        Assert.True(decision.Result.TableClosed);
    }

    private static PokerExecutionState State() => new(
        new PokerTable
        {
            InviteCode = "ABCDE", ChatId = 10, HostUserId = 1,
            Status = PokerTableStatus.Seating, SmallBlind = 1, BigBlind = 2,
            LastActionAt = Now.ToUnixTimeMilliseconds(), CreatedAt = Now.ToUnixTimeMilliseconds(),
        },
        [Seat(1, 0)],
        500);

    private static PokerSeat Seat(long userId, int position) => new()
    {
        InviteCode = "ABCDE", UserId = userId, Position = position,
        DisplayName = $"p{userId}", Stack = 100, ChatId = 10,
        JoinedAt = Now.ToUnixTimeMilliseconds(),
    };

    private static GameActionInput<PokerExecutionState, TCommand> Input<TCommand>(
        TCommand command, PokerExecutionState state, IReadOnlyDictionary<string, double>? entropy = null) =>
        new(command, state, new WalletSnapshot(0), new Dictionary<string, QuotaSnapshot>(),
            new EntropyValue(entropy ?? new Dictionary<string, double>()), Now);
}
