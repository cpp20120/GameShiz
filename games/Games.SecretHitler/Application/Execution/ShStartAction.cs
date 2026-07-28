using BotFramework.Sdk.Execution;
using static Games.SecretHitler.Domain.Rules.ShResultHelpers;

namespace Games.SecretHitler.Application.Execution;

public sealed class ShStartAction : IGameAction<ShStartCommand, SecretHitlerExecutionState, ShStartResult>
{
    public GameDecision<SecretHitlerExecutionState, ShStartResult> Decide(
        GameActionInput<SecretHitlerExecutionState, ShStartCommand> input)
    {
        if (input.State.Game is not { } source || input.State.Players.All(p => p.UserId != input.Command.ActorUserId))
            return Reject(input.State, ShError.NotInGame);
        if (source.HostUserId != input.Command.ActorUserId) return Reject(input.State, ShError.NotHost);
        if (source.Status != ShStatus.Lobby) return Reject(input.State, ShError.GameInProgress);
        if (input.State.Players.Count < ShRoleDealer.MinPlayers) return Reject(input.State, ShError.NotEnoughPlayers);
        var state = SecretHitlerExecutionRules.Clone(input.State);
        ShTransitions.StartGame(state.Game!, state.Players,
            SecretHitlerExecutionRules.RoleEntropyNames.Select(input.Entropy.GetDouble).ToArray(),
            SecretHitlerExecutionRules.DeckEntropyNames.Select(input.Entropy.GetDouble).ToArray());
        state.Game!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        return new(DecisionStatus.Accepted, state,
            new(ShError.None, SecretHitlerExecutionRules.Snapshot(state)), [], [], [],
            [new SecretHitlerGameStarted(source.InviteCode, state.Players.Count, state.Game.LastActionAt)], []);
    }

    private static GameDecision<SecretHitlerExecutionState, ShStartResult> Reject(
        SecretHitlerExecutionState state, ShError error) =>
        new(DecisionStatus.Rejected, state, StartFail(error), [], [], [], [], [], error.ToString());
}
