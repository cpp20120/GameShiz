using BotFramework.Sdk.Execution;

namespace Games.Blackjack.Application.Execution;

public sealed class BlackjackSetMessageAction
    : IGameAction<BlackjackSetMessageCommand, BlackjackGameState, BlackjackResult>
{
    public GameDecision<BlackjackGameState, BlackjackResult> Decide(
        GameActionInput<BlackjackGameState, BlackjackSetMessageCommand> input)
    {
        var hand = input.State.Hand;
        if (input.State.Status != TurnGameStatus.Active
            || hand is null
            || !string.Equals(hand.HandId, input.Command.HandId, StringComparison.Ordinal))
        {
            return new GameDecision<BlackjackGameState, BlackjackResult>(
                DecisionStatus.Rejected,
                input.State,
                new BlackjackResult(BlackjackError.NoActiveHand, null),
                [],
                [],
                [],
                [],
                [],
                "hand_not_active");
        }

        var updated = hand with { StateMessageId = input.Command.MessageId };
        return new GameDecision<BlackjackGameState, BlackjackResult>(
            DecisionStatus.Accepted,
            input.State with { Revision = input.State.Revision + 1, Hand = updated },
            new BlackjackResult(
                BlackjackError.None,
                BlackjackDecisionRules.BuildSnapshot(updated, input.Wallet.Balance, false),
                input.Command.MessageId),
            [],
            [],
            [],
            [new BlackjackStateMessageSet(
                input.Command.UserId,
                input.Command.MessageId,
                input.UtcNow.ToUnixTimeMilliseconds())],
            []);
    }
}
