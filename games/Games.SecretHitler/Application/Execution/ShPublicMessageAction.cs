using BotFramework.Sdk.Execution;
using static Games.SecretHitler.Domain.Rules.ShResultHelpers;

namespace Games.SecretHitler.Application.Execution;

public sealed class ShPublicMessageAction
    : IGameAction<ShPublicMessageCommand, SecretHitlerExecutionState, bool>
{
    public GameDecision<SecretHitlerExecutionState, bool> Decide(
        GameActionInput<SecretHitlerExecutionState, ShPublicMessageCommand> input)
    {
        if (input.State.Game is null)
            return new(DecisionStatus.Rejected, input.State, false, [], [], [], [], [], "game_not_found");
        var state = SecretHitlerExecutionRules.Clone(input.State);
        state.Game!.StateMessageId = input.Command.MessageId;
        return new(DecisionStatus.Accepted, state, true, [], [], [], [], []);
    }
}
