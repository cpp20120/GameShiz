using BotFramework.Sdk.Execution;
using Games.Poker.Application.Execution;
using Xunit;

using PokerDeck = Games.Poker.Domain.Rules.Deck;

namespace CasinoShiz.Tests;

public sealed class PokerExecutionRulesTests
{
    [Theory]
    [InlineData(ValidationResult.CannotCheck, PokerError.CannotCheck)]
    [InlineData(ValidationResult.RaiseTooSmall, PokerError.RaiseTooSmall)]
    [InlineData(ValidationResult.RaiseTooLarge, PokerError.RaiseTooLarge)]
    [InlineData(ValidationResult.Ok, PokerError.InvalidAction)]
    public void MapValidation_PreservesActionSpecificErrors(
        ValidationResult validation,
        PokerError expected)
    {
        Assert.Equal(expected, PokerExecutionRules.MapValidation(validation));
    }

    [Fact]
    public void Clone_DeepCopiesMutableTableAndSeats()
    {
        var state = new PokerExecutionState(
            new PokerTable
            {
                InviteCode = "ABCDE",
                ChatId = 10,
                HostUserId = 1,
                Pot = 25,
                CommunityCards = "AS KD",
            },
            [new PokerSeat
            {
                InviteCode = "ABCDE",
                Position = 0,
                UserId = 1,
                DisplayName = "alice",
                Stack = 100,
                HoleCards = "2C 3C",
            }],
            500);

        var clone = PokerExecutionRules.Clone(state);

        clone.Table!.Pot = 100;
        clone.Table.CommunityCards = "QH JS";
        clone.Seats[0].Stack = 40;
        clone.Seats[0].DisplayName = "changed";
        clone.Seats.Add(new PokerSeat { UserId = 2, Position = 1 });

        Assert.Equal(25, state.Table!.Pot);
        Assert.Equal("AS KD", state.Table.CommunityCards);
        Assert.Equal(100, state.Seats[0].Stack);
        Assert.Equal("alice", state.Seats[0].DisplayName);
        Assert.Single(state.Seats);
        Assert.Equal(500, clone.ActorBalance);
        Assert.NotSame(state.Table, clone.Table);
        Assert.NotSame(state.Seats[0], clone.Seats[0]);
    }

    [Fact]
    public void Resolve_WhenBettingRoundIsIncomplete_AdvancesToNextSeatWithoutEffects()
    {
        var table = new PokerTable
        {
            InviteCode = "ABCDE", Status = PokerTableStatus.HandActive,
            Phase = PokerPhase.PreFlop, CurrentSeat = 0, CurrentBet = 2,
            BigBlind = 2, MinRaise = 2,
        };
        var state = new PokerExecutionState(table,
        [
            Seat(1, 0, currentBet: 2, hasActedThisRound: true),
            Seat(2, 1),
        ], 0);

        var resolution = PokerExecutionRules.Resolve(state, Now);

        Assert.Equal(HandTransition.TurnAdvanced, resolution.Result.Transition);
        Assert.Equal(1, table.CurrentSeat);
        Assert.Empty(resolution.Effects);
        Assert.Empty(resolution.Events);
    }

    [Fact]
    public void Resolve_WhenBettingRoundCompletes_AdvancesPhaseWithoutPayout()
    {
        var table = new PokerTable
        {
            InviteCode = "ABCDE", Status = PokerTableStatus.HandActive,
            Phase = PokerPhase.PreFlop, CurrentSeat = 0, CurrentBet = 2,
            BigBlind = 2, MinRaise = 2, DeckState = PokerDeck.BuildShuffled(),
        };
        var state = new PokerExecutionState(table,
        [
            Seat(1, 0, currentBet: 2, hasActedThisRound: true),
            Seat(2, 1, currentBet: 2, hasActedThisRound: true),
        ], 0);

        var resolution = PokerExecutionRules.Resolve(state, Now);

        Assert.Equal(HandTransition.PhaseAdvanced, resolution.Result.Transition);
        Assert.Equal(PokerPhase.Flop, table.Phase);
        Assert.Equal(3, PokerDeck.Parse(table.CommunityCards).Length);
        Assert.Empty(resolution.Effects);
        Assert.Empty(resolution.Events);
    }

    [Fact]
    public void Resolve_WhenOnlyOnePlayerRemains_CreditsWinnerAndEmitsEvent()
    {
        var table = new PokerTable
        {
            InviteCode = "ABCDE", Status = PokerTableStatus.HandActive,
            Phase = PokerPhase.Turn, Pot = 50,
        };
        var winner = Seat(1, 0);
        var state = new PokerExecutionState(table,
        [
            winner,
            Seat(2, 1, status: PokerSeatStatus.Folded, stack: 0),
        ], 0);

        var resolution = PokerExecutionRules.Resolve(state, Now);

        Assert.Equal(HandTransition.HandEnded, resolution.Result.Transition);
        var showdown = Assert.Single(resolution.Result.Showdown!);
        Assert.Same(winner, showdown.Seat);
        Assert.Equal(50, showdown.Won);
        Assert.Equal(150, winner.Stack);
        var payout = Assert.IsType<WalletEconomyEffect>(Assert.Single(resolution.Effects));
        Assert.Equal(1, payout.UserId);
        Assert.Equal(50, payout.Amount);
        var ended = Assert.IsType<PokerHandEnded>(Assert.Single(resolution.Events));
        Assert.Equal("last_standing", ended.Reason);
        Assert.Equal(50, ended.Winners.Single().Amount);
    }

    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    private static PokerSeat Seat(
        long userId,
        int position,
        int stack = 100,
        int currentBet = 0,
        bool hasActedThisRound = false,
        PokerSeatStatus status = PokerSeatStatus.Seated) => new()
    {
        InviteCode = "ABCDE", Position = position, UserId = userId,
        DisplayName = $"p{userId}", Stack = stack, ChatId = 10,
        Status = status, CurrentBet = currentBet,
        HasActedThisRound = hasActedThisRound,
    };
}
