using BotFramework.Sdk.Execution;
using static Games.SecretHitler.Domain.Rules.ShResultHelpers;

namespace Games.SecretHitler.Application.Execution;

public sealed class ShDiscardAction : IGameAction<ShDiscardCommand, SecretHitlerExecutionState, ShDiscardResult>
{
    public GameDecision<SecretHitlerExecutionState, ShDiscardResult> Decide(
        GameActionInput<SecretHitlerExecutionState, ShDiscardCommand> input)
    {
        if (input.State.Game is null) return Reject(input.State, ShError.NotInGame);
        var state = SecretHitlerExecutionRules.Clone(input.State);
        var actor = state.Players.FirstOrDefault(p => p.UserId == input.Command.ActorUserId);
        if (actor is null) return Reject(input.State, ShError.NotInGame);
        var validation = ShTransitions.ValidatePresidentDiscard(state.Game!, actor, input.Command.DiscardIndex);
        if (validation != ShValidation.Ok) return Reject(input.State, MapValidation(validation));
        ShTransitions.ApplyPresidentDiscard(state.Game!, input.Command.DiscardIndex);
        state.Game!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        return new(DecisionStatus.Accepted, state,
            new(ShError.None, SecretHitlerExecutionRules.Snapshot(state)), [], [], [], [], []);
    }

    private static GameDecision<SecretHitlerExecutionState, ShDiscardResult> Reject(
        SecretHitlerExecutionState state, ShError error) =>
        new(DecisionStatus.Rejected, state, DiscardFail(error), [], [], [], [], [], error.ToString());
}
