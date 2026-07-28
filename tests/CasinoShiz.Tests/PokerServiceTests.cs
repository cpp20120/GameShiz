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

    [Fact]
    public void CreateDecision_RejectsExistingTableWithoutMutation()
    {
        var state = State();
        var command = new PokerCreateCommand(1, "alice", 10, "create", 100, 1, 2, []);

        var decision = new PokerCreateAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(PokerError.TableAlreadyExists, decision.Result.Error);
        Assert.Same(state, decision.NewState);
        Assert.Null(decision.CustomEffects);
    }

    [Fact]
    public void CreateDecision_RejectsWhenBalanceIsInsufficient()
    {
        var state = new PokerExecutionState(null, [], 99);
        var command = new PokerCreateCommand(1, "alice", 10, "create", 100, 1, 2, []);

        var decision = new PokerCreateAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(PokerError.NotEnoughCoins, decision.Result.Error);
        Assert.Same(state, decision.NewState);
    }

    [Fact]
    public void JoinDecision_RejectsWrongChatAndDoesNotMutateState()
    {
        var state = State();
        var command = new PokerJoinCommand("ABCDE", 2, "bob", 99, "join", 100, 6, []);

        var decision = new PokerJoinAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(PokerError.TableNotFound, decision.Result.Error);
        Assert.Single(state.Seats);
    }

    [Fact]
    public void JoinDecision_RejectsWhenHandIsActive()
    {
        var state = State();
        state.Table!.Status = PokerTableStatus.HandActive;
        var command = new PokerJoinCommand("ABCDE", 2, "bob", 10, "join", 100, 6, []);

        var decision = new PokerJoinAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(PokerError.HandInProgress, decision.Result.Error);
    }

    [Fact]
    public void JoinDecision_RejectsDuplicatePlayerBeforeBalanceAndCapacityChecks()
    {
        var state = State();
        var command = new PokerJoinCommand("ABCDE", 1, "alice", 10, "join", 1000, 1, []);

        var decision = new PokerJoinAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(PokerError.AlreadySeated, decision.Result.Error);
    }

    [Fact]
    public void JoinDecision_RejectsInsufficientBalance()
    {
        var state = State();
        var command = new PokerJoinCommand("ABCDE", 2, "bob", 10, "join", 501, 6, []);

        var decision = new PokerJoinAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(PokerError.NotEnoughCoins, decision.Result.Error);
    }

    [Fact]
    public void JoinDecision_RejectsFullTable()
    {
        var state = State();
        var command = new PokerJoinCommand("ABCDE", 2, "bob", 10, "join", 100, 1, []);

        var decision = new PokerJoinAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(PokerError.TableFull, decision.Result.Error);
    }

    [Fact]
    public void StartDecision_RejectsNonHostAndTooFewPlayers()
    {
        var state = State();
        state.Seats.Add(Seat(2, 1));
        var nonHost = new PokerStartCommand("ABCDE", 2, "bob", 10, "start", []);

        var nonHostDecision = new PokerStartAction().Decide(Input(nonHost, state));

        Assert.Equal(PokerError.NotHost, nonHostDecision.Result.Error);

        state.Seats.RemoveAt(1);
        var host = new PokerStartCommand("ABCDE", 1, "alice", 10, "start", []);
        var tooFewDecision = new PokerStartAction().Decide(Input(host, state));

        Assert.Equal(PokerError.NeedTwo, tooFewDecision.Result.Error);
    }

    [Fact]
    public void PlayerTurn_RejectsUnknownVerbAndCannotCheck()
    {
        var state = State();
        state.Seats.Add(Seat(2, 1));
        state.Table!.Status = PokerTableStatus.HandActive;
        state.Table.CurrentSeat = 0;
        state.Table.CurrentBet = 2;

        var unknown = new PokerPlayerTurnCommand("ABCDE", 1, "alice", 10, "turn", "wat", 0, []);
        var unknownDecision = new PokerPlayerTurnAction().Decide(Input(unknown, state));
        Assert.Equal(PokerError.InvalidAction, unknownDecision.Result.Error);

        var cannotCheck = unknown with { Verb = "check" };
        var cannotCheckDecision = new PokerPlayerTurnAction().Decide(Input(cannotCheck, state));
        Assert.Equal(PokerError.CannotCheck, cannotCheckDecision.Result.Error);
    }

    [Theory]
    [InlineData(1, PokerError.RaiseTooSmall)]
    [InlineData(101, PokerError.RaiseTooLarge)]
    public void PlayerTurn_MapsRaiseValidationErrors(int amount, PokerError expected)
    {
        var state = State();
        state.Seats.Add(Seat(2, 1));
        state.Table!.Status = PokerTableStatus.HandActive;
        state.Table.CurrentSeat = 0;
        state.Table.CurrentBet = 2;
        var command = new PokerPlayerTurnCommand("ABCDE", 1, "alice", 10, "turn", "raise", amount, []);

        var decision = new PokerPlayerTurnAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(expected, decision.Result.Error);
        Assert.Same(state, decision.NewState);
    }

    [Fact]
    public void AutoTurn_ChecksWhenNoCallIsNeeded()
    {
        var state = State();
        state.Seats.Add(Seat(2, 1));
        state.Table!.Status = PokerTableStatus.HandActive;
        state.Table.CurrentSeat = 0;
        state.Table.CurrentBet = 0;

        var command = new PokerAutoTurnCommand("ABCDE", 0, "system", 10, "timeout", []);
        var decision = new PokerAutoTurnAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(AutoAction.Check, decision.Result.AutoKind);
        Assert.Equal("p1", decision.Result.AutoActorName);
    }

    [Fact]
    public void LeaveDecision_DuringHandFoldsPlayerAndResolvesWinner()
    {
        var state = State();
        state.Seats.Add(Seat(2, 1));
        state.Table!.Status = PokerTableStatus.HandActive;
        state.Table.Pot = 20;
        var command = new PokerLeaveCommand("ABCDE", 1, "alice", 10, "leave", []);

        var decision = new PokerLeaveAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.DoesNotContain(decision.NewState.Seats, seat => seat.UserId == 1);
        Assert.Contains(decision.CustomEffects!, effect =>
            effect is WalletEconomyEffect { UserId: 1, Amount: 100, Kind: EconomyEffectKind.Credit });
        Assert.Contains(decision.CustomEffects!, effect =>
            effect is WalletEconomyEffect { UserId: 2, Amount: 20, Reason: "poker.win" });
        Assert.Contains(decision.Events, @event =>
            @event is PokerHandEnded { Reason: "last_standing" });
    }

    [Fact]
    public void SetMessageDecision_UpdatesTableMessageOnClonedState()
    {
        var state = State();
        var command = new PokerSetMessageCommand("ABCDE", 1, "alice", 10, "message", 42, []);

        var decision = new PokerSetMessageAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.True(decision.Result);
        Assert.Equal(42, decision.NewState.Table!.StateMessageId);
        Assert.Null(state.Table!.StateMessageId);
    }

    [Fact]
    public void SetMessageDecision_RejectsMissingTable()
    {
        var state = new PokerExecutionState(null, [], 0);
        var command = new PokerSetMessageCommand("ABCDE", 1, "alice", 10, "message", 42, []);

        var decision = new PokerSetMessageAction().Decide(Input(command, state));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.False(decision.Result);
        Assert.Equal("no_table", decision.RejectionReason);
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
