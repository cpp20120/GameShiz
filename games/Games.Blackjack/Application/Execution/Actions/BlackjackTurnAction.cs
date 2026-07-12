using BotFramework.Sdk.Execution;

namespace Games.Blackjack.Application.Execution;

public sealed class BlackjackTurnAction
    : TurnBasedGameAction<BlackjackTurnCommand, BlackjackGameState, BlackjackResult, long>
{
    protected override GameDecision<BlackjackGameState, BlackjackResult> DecideTurn(
        GameActionInput<BlackjackGameState, BlackjackTurnCommand> input)
    {
        var hand = input.State.Hand
            ?? throw new InvalidOperationException("An active blackjack state has no hand.");
        return input.Command.Kind switch
        {
            BlackjackTurnKind.Hit => Hit(input, hand),
            BlackjackTurnKind.Stand => Settle(input, hand, doubled: false, additionalDebit: 0),
            BlackjackTurnKind.DoubleDown => Double(input, hand),
            _ => throw new InvalidOperationException($"Unknown blackjack turn '{input.Command.Kind}'."),
        };
    }

    protected override BlackjackResult CreateRejectedResult(TurnRejection<long> rejection) =>
        new(BlackjackError.NoActiveHand, null);

    private static GameDecision<BlackjackGameState, BlackjackResult> Hit(
        GameActionInput<BlackjackGameState, BlackjackTurnCommand> input,
        BlackjackHandState hand)
    {
        var deck = hand.DeckState;
        var card = Deck.Draw(ref deck, 1)[0];
        var updated = hand with
        {
            PlayerCards = [.. hand.PlayerCards, card],
            DeckState = deck,
        };
        if (BlackjackHandValue.Compute(updated.PlayerCards) > 21)
            return Settle(input, updated, doubled: false, additionalDebit: 0);

        var occurredAt = input.UtcNow.ToUnixTimeMilliseconds();
        return new GameDecision<BlackjackGameState, BlackjackResult>(
            DecisionStatus.Accepted,
            input.State with { Revision = input.State.Revision + 1, Hand = updated },
            new BlackjackResult(
                BlackjackError.None,
                BlackjackDecisionRules.BuildSnapshot(updated, input.Wallet.Balance, false),
                updated.StateMessageId),
            [],
            [],
            [],
            [new BlackjackHandUpdated(
                updated.UserId,
                updated.ChatId,
                updated.Bet,
                string.Join(' ', updated.PlayerCards),
                string.Join(' ', updated.DealerCards),
                updated.DeckState,
                updated.StateMessageId,
                updated.CreatedAt.ToUnixTimeMilliseconds(),
                occurredAt)],
            []);
    }

    private static GameDecision<BlackjackGameState, BlackjackResult> Double(
        GameActionInput<BlackjackGameState, BlackjackTurnCommand> input,
        BlackjackHandState hand)
    {
        if (hand.PlayerCards.Length != 2)
            return Reject(input.State, BlackjackError.CannotDouble, "cannot_double");
        if (input.Wallet.Balance < hand.Bet)
            return Reject(input.State, BlackjackError.NotEnoughCoins, "insufficient_balance");

        var deck = hand.DeckState;
        var card = Deck.Draw(ref deck, 1)[0];
        var originalBet = hand.Bet;
        var doubled = hand with
        {
            Bet = checked(originalBet * 2),
            PlayerCards = [.. hand.PlayerCards, card],
            DeckState = deck,
        };
        return Settle(input, doubled, doubled: true, additionalDebit: originalBet);
    }

    private static GameDecision<BlackjackGameState, BlackjackResult> Settle(
        GameActionInput<BlackjackGameState, BlackjackTurnCommand> input,
        BlackjackHandState hand,
        bool doubled,
        int additionalDebit)
    {
        var settlement = BlackjackDecisionRules.Settle(
            hand,
            doubled,
            input.Wallet.Balance,
            additionalDebit,
            input.UtcNow);
        var economy = new List<EconomyEffect>();
        if (additionalDebit > 0)
            economy.Add(EconomyEffect.Debit(additionalDebit, "blackjack.double"));
        if (settlement.Payout > 0)
            economy.Add(EconomyEffect.Credit(settlement.Payout, "blackjack.settle"));
        return new GameDecision<BlackjackGameState, BlackjackResult>(
            DecisionStatus.Accepted,
            input.State with
            {
                Revision = input.State.Revision + 1,
                Status = TurnGameStatus.Completed,
                TurnDeadline = null,
                Hand = null,
            },
            new BlackjackResult(BlackjackError.None, settlement.Snapshot, hand.StateMessageId),
            economy,
            [],
            [],
            [settlement.Completed, settlement.Closed],
            [ScheduleEffect.Cancel("hand-timeout")]);
    }

    private static GameDecision<BlackjackGameState, BlackjackResult> Reject(
        BlackjackGameState state,
        BlackjackError error,
        string reason) =>
        new(DecisionStatus.Rejected, state, new BlackjackResult(error, null), [], [], [], [], [], reason);
}
