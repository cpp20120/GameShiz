using BotFramework.Sdk.Execution;

namespace Games.Blackjack.Application.Execution;

public sealed class BlackjackTimeoutAction
    : IGameAction<BlackjackTimeoutCommand, BlackjackGameState, BlackjackResult>
{
    public GameDecision<BlackjackGameState, BlackjackResult> Decide(
        GameActionInput<BlackjackGameState, BlackjackTimeoutCommand> input)
    {
        var hand = input.State.Hand;
        if (input.State.Status != TurnGameStatus.Active
            || hand is null
            || !string.Equals(hand.HandId, input.Command.HandId, StringComparison.Ordinal))
        {
            return Reject(input.State, "hand_not_active");
        }
        if (input.State.TurnDeadline is { } deadline && input.UtcNow < deadline)
            throw new InvalidOperationException("Blackjack timeout fired before its deadline.");

        var settlement = BlackjackDecisionRules.Settle(
            hand,
            doubled: false,
            input.Wallet.Balance,
            additionalDebit: 0,
            input.UtcNow);
        var economy = settlement.Payout > 0
            ? new[] { EconomyEffect.Credit(settlement.Payout, "blackjack.settle") }
            : [];
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
        string reason) =>
        new(
            DecisionStatus.Rejected,
            state,
            new BlackjackResult(BlackjackError.NoActiveHand, null),
            [],
            [],
            [],
            [],
            [],
            reason);
}
