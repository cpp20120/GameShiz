using BotFramework.Sdk.Execution;
using static Games.SecretHitler.Domain.Rules.ShResultHelpers;

namespace Games.SecretHitler.Application.Execution;

public sealed class ShPlayerMessageAction
    : IGameAction<ShPlayerMessageCommand, SecretHitlerExecutionState, bool>
{
    public GameDecision<SecretHitlerExecutionState, bool> Decide(
        GameActionInput<SecretHitlerExecutionState, ShPlayerMessageCommand> input)
    {
        var state = SecretHitlerExecutionRules.Clone(input.State);
        var actor = state.Players.FirstOrDefault(p => p.UserId == input.Command.ActorUserId);
        if (actor is null) return new(DecisionStatus.Rejected, input.State, false, [], [], [], [], [], "not_in_game");
        actor.StateMessageId = input.Command.MessageId;
        return new(DecisionStatus.Accepted, state, true, [], [], [], [], []);
    }
}
