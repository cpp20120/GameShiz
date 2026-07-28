using BotFramework.Sdk.Execution;
using static Games.SecretHitler.Domain.Rules.ShResultHelpers;

namespace Games.SecretHitler.Application.Execution;

public sealed class ShNominateAction : IGameAction<ShNominateCommand, SecretHitlerExecutionState, ShNominateResult>
{
    public GameDecision<SecretHitlerExecutionState, ShNominateResult> Decide(
        GameActionInput<SecretHitlerExecutionState, ShNominateCommand> input)
    {
        if (input.State.Game is null) return Reject(input.State, ShError.NotInGame);
        var state = SecretHitlerExecutionRules.Clone(input.State);
        var actor = state.Players.FirstOrDefault(p => p.UserId == input.Command.ActorUserId);
        if (actor is null) return Reject(input.State, ShError.NotInGame);
        var validation = ShTransitions.ValidateNomination(state.Game!, actor,
            input.Command.ChancellorPosition, state.Players);
        if (validation != ShValidation.Ok) return Reject(input.State, MapValidation(validation));
        ShTransitions.ApplyNomination(state.Game!, input.Command.ChancellorPosition, state.Players);
        state.Game!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        return new(DecisionStatus.Accepted, state,
            new(ShError.None, SecretHitlerExecutionRules.Snapshot(state)), [], [], [], [], []);
    }

    private static GameDecision<SecretHitlerExecutionState, ShNominateResult> Reject(
        SecretHitlerExecutionState state, ShError error) =>
        new(DecisionStatus.Rejected, state, NominateFail(error), [], [], [], [], [], error.ToString());
}
