using BotFramework.Sdk.Execution;
using Games.Blackjack.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class BlackjackResolveTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("KS KH", "8S 5H", "4S", BlackjackOutcome.PlayerWin, 20)]
    [InlineData("KS KH", "9S 7H", "KD", BlackjackOutcome.DealerBust, 20)]
    [InlineData("KS QH", "KD JH", "2S", BlackjackOutcome.Push, 10)]
    [InlineData("7S 8H", "KD 6H", "5S", BlackjackOutcome.DealerWin, 0)]
    public void Stand_ResolvesExpectedOutcome(
        string player,
        string dealer,
        string deck,
        BlackjackOutcome outcome,
        int payout)
    {
        var decision = Turn(
            BlackjackTurnKind.Stand,
            player.Split(' '),
            dealer.Split(' '),
            deck,
            balance: 100);

        Assert.Equal(outcome, decision.Result.Snapshot!.Outcome);
        Assert.Equal(payout, decision.Result.Snapshot.Payout);
        Assert.Equal(TurnGameStatus.Completed, decision.NewState.Status);
    }

    [Fact]
    public void Hit_PlayerBustSettlesWithoutCredit()
    {
        var decision = Turn(
            BlackjackTurnKind.Hit,
            ["9S", "7H"],
            ["8D", "5C"],
            "KD 2C");

        Assert.Equal(BlackjackOutcome.PlayerBust, decision.Result.Snapshot!.Outcome);
        Assert.DoesNotContain(decision.Economy, effect => effect.Kind == EconomyEffectKind.Credit);
    }

    [Fact]
    public void Double_MoreThanTwoCardsRejectsWithoutDebit()
    {
        var decision = Turn(
            BlackjackTurnKind.DoubleDown,
            ["5S", "6H", "3D"],
            ["8D", "5C"],
            "KD 2C");

        Assert.Equal(BlackjackError.CannotDouble, decision.Result.Error);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void Double_InsufficientFundsRejectsWithoutDebit()
    {
        var decision = Turn(
            BlackjackTurnKind.DoubleDown,
            ["5S", "6H"],
            ["8D", "5C"],
            "KD 2C",
            bet: 100,
            balance: 5);

        Assert.Equal(BlackjackError.NotEnoughCoins, decision.Result.Error);
        Assert.Empty(decision.Economy);
    }

    private static GameDecision<BlackjackGameState, BlackjackResult> Turn(
        BlackjackTurnKind kind,
        string[] player,
        string[] dealer,
        string deck,
        int bet = 10,
        long balance = 100)
    {
        var state = new BlackjackGameState(
            2,
            TurnGameStatus.Active,
            1,
            Now.AddMinutes(1),
            "player",
            new BlackjackHandState("hand", 1, 100, bet, player, dealer, deck, null, Now));
        var command = new BlackjackTurnCommand(1, "player", 100, kind, 2, $"turn:{kind}");
        return new BlackjackTurnAction().Decide(
            new GameActionInput<BlackjackGameState, BlackjackTurnCommand>(
                command,
                state,
                new WalletSnapshot(balance),
                new Dictionary<string, QuotaSnapshot>(),
                EntropyValue.Empty,
                Now));
    }
}
