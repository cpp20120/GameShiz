using BotFramework.Sdk.Execution;

namespace Games.Blackjack.Application.Execution;

public sealed class BlackjackStartAction
    : IGameAction<BlackjackStartCommand, BlackjackGameState, BlackjackResult>
{
    public GameDecision<BlackjackGameState, BlackjackResult> Decide(
        GameActionInput<BlackjackGameState, BlackjackStartCommand> input)
    {
        var command = input.Command;
        if (command.Bet < command.MinBet || command.Bet > command.MaxBet)
            return Reject(input.State, BlackjackError.InvalidBet, "invalid_bet");
        if (input.State.Status == TurnGameStatus.Active || input.State.Hand is not null)
            return Reject(input.State, BlackjackError.HandInProgress, "hand_in_progress");
        if (input.Wallet.Balance < command.Bet)
            return Reject(input.State, BlackjackError.NotEnoughCoins, "insufficient_balance");

        var deck = BlackjackDecisionRules.BuildShuffledDeck(input.Entropy);
        var player = Deck.Draw(ref deck, 2);
        var dealer = Deck.Draw(ref deck, 2);
        var deadline = input.UtcNow.AddMilliseconds(command.HandTimeoutMs);
        var hand = new BlackjackHandState(
            command.CommandId,
            command.UserId,
            command.ChatId,
            command.Bet,
            player,
            dealer,
            deck,
            null,
            input.UtcNow);
        var started = new BlackjackHandStarted(
            command.UserId,
            command.ChatId,
            command.Bet,
            string.Join(' ', player),
            string.Join(' ', dealer),
            deck,
            null,
            input.UtcNow.ToUnixTimeMilliseconds(),
            input.UtcNow.ToUnixTimeMilliseconds());
        var debit = EconomyEffect.Debit(command.Bet, "blackjack.start");

        if (BlackjackHandValue.IsNaturalBlackjack(player))
        {
            var settlement = BlackjackDecisionRules.Settle(
                hand,
                doubled: false,
                input.Wallet.Balance - command.Bet,
                additionalDebit: 0,
                input.UtcNow);
            var economy = settlement.Payout > 0
                ? new[] { debit, EconomyEffect.Credit(settlement.Payout, "blackjack.settle") }
                : [debit];
            return new GameDecision<BlackjackGameState, BlackjackResult>(
                DecisionStatus.Accepted,
                input.State with
                {
                    Revision = input.State.Revision + 1,
                    Status = TurnGameStatus.Completed,
                    CurrentPlayerId = command.UserId,
                    TurnDeadline = null,
                    DisplayName = command.DisplayName,
                    Hand = null,
                },
                new BlackjackResult(BlackjackError.None, settlement.Snapshot),
                economy,
                [],
                [],
                [started, settlement.Completed, settlement.Closed],
                []);
        }

        var newState = input.State with
        {
            Revision = input.State.Revision + 1,
            Status = TurnGameStatus.Active,
            CurrentPlayerId = command.UserId,
            TurnDeadline = deadline,
            DisplayName = command.DisplayName,
            Hand = hand,
        };
        var timeout = new BlackjackTimeoutCommand(
            command.UserId,
            command.DisplayName,
            command.ChatId,
            hand.HandId,
            $"{command.CommandId}:timeout");
        return new GameDecision<BlackjackGameState, BlackjackResult>(
            DecisionStatus.Accepted,
            newState,
            new BlackjackResult(
                BlackjackError.None,
                BlackjackDecisionRules.BuildSnapshot(hand, input.Wallet.Balance - command.Bet, false)),
            [debit],
            [],
            [],
            [started],
            [ScheduleEffect.ScheduleCommand("hand-timeout", deadline, timeout)]);
    }

    private static GameDecision<BlackjackGameState, BlackjackResult> Reject(
        BlackjackGameState state,
        BlackjackError error,
        string reason) =>
        new(DecisionStatus.Rejected, state, new BlackjackResult(error, null), [], [], [], [], [], reason);
}
