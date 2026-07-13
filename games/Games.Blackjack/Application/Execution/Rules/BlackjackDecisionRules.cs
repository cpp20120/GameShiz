using BotFramework.Sdk.Execution;

namespace Games.Blackjack.Application.Execution;

internal static class BlackjackDecisionRules
{
    private static readonly string[] Suits = ["S", "H", "D", "C"];
    private static readonly string[] Ranks = ["2", "3", "4", "5", "6", "7", "8", "9", "T", "J", "Q", "K", "A"];

    public static IReadOnlyList<string> ShuffleEntropyNames { get; } =
        Enumerable.Range(1, 51).Select(static index => $"shuffle-{index}").ToArray();

    public static string BuildShuffledDeck(EntropyValue entropy)
    {
        var cards = (from suit in Suits from rank in Ranks select rank + suit).ToArray();
        for (var index = cards.Length - 1; index > 0; index--)
        {
            var random = entropy.GetDouble($"shuffle-{index}");
            var swap = Math.Min(index, (int)(random * (index + 1)));
            (cards[index], cards[swap]) = (cards[swap], cards[index]);
        }
        return string.Join(' ', cards);
    }

    public static BlackjackSnapshot BuildSnapshot(
        BlackjackHandState hand,
        long balance,
        bool revealed,
        BlackjackOutcome? outcome = null,
        int payout = 0)
    {
        var dealerCards = revealed ? hand.DealerCards : hand.DealerCards.Take(1).ToArray();
        return new BlackjackSnapshot(
            hand.PlayerCards,
            dealerCards,
            BlackjackHandValue.Compute(hand.PlayerCards),
            BlackjackHandValue.Compute(dealerCards),
            hand.Bet,
            checked((int)balance),
            revealed,
            !revealed && hand.PlayerCards.Length == 2 && balance >= hand.Bet,
            outcome,
            payout);
    }

    public static BlackjackSettlement Settle(
        BlackjackHandState hand,
        bool doubled,
        long balanceBeforeEffects,
        int additionalDebit,
        DateTimeOffset utcNow)
    {
        var dealer = hand.DealerCards.ToList();
        var deck = hand.DeckState;
        var playerTotal = BlackjackHandValue.Compute(hand.PlayerCards);
        var playerBlackjack = !doubled && BlackjackHandValue.IsNaturalBlackjack(hand.PlayerCards);
        if (playerTotal <= 21)
        {
            while (BlackjackHandValue.Compute(dealer) < 17)
            {
                var drawn = Deck.Draw(ref deck, 1);
                dealer.Add(drawn[0]);
            }
        }

        var dealerTotal = BlackjackHandValue.Compute(dealer);
        var dealerBlackjack = BlackjackHandValue.IsNaturalBlackjack(dealer);
        var (outcome, payout) = Resolve(playerTotal, dealerTotal, playerBlackjack, dealerBlackjack, hand.Bet);
        var settledHand = hand with { DealerCards = dealer.ToArray(), DeckState = deck };
        var finalBalance = checked(balanceBeforeEffects - additionalDebit + payout);
        var occurredAt = utcNow.ToUnixTimeMilliseconds();
        return new BlackjackSettlement(
            BuildSnapshot(settledHand, finalBalance, true, outcome, payout),
            payout,
            new BlackjackHandCompleted(
                hand.UserId,
                hand.ChatId,
                hand.Bet,
                payout,
                playerTotal,
                dealerTotal,
                outcome.ToString(),
                doubled,
                occurredAt),
            new BlackjackHandClosed(
                hand.UserId,
                hand.ChatId,
                hand.CreatedAt.ToUnixTimeMilliseconds(),
                "settled",
                occurredAt));
    }

    private static (BlackjackOutcome Outcome, int Payout) Resolve(
        int playerTotal,
        int dealerTotal,
        bool playerBlackjack,
        bool dealerBlackjack,
        int bet)
    {
        if (playerTotal > 21) return (BlackjackOutcome.PlayerBust, 0);
        if (playerBlackjack && !dealerBlackjack) return (BlackjackOutcome.PlayerBlackjack, bet + (bet * 3 / 2));
        if (playerBlackjack && dealerBlackjack) return (BlackjackOutcome.Push, bet);
        if (dealerTotal > 21) return (BlackjackOutcome.DealerBust, bet * 2);
        if (playerTotal > dealerTotal) return (BlackjackOutcome.PlayerWin, bet * 2);
        if (playerTotal < dealerTotal) return (BlackjackOutcome.DealerWin, 0);
        return (BlackjackOutcome.Push, bet);
    }
}
