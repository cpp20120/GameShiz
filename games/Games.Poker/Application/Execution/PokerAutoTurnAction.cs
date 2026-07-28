using BotFramework.Sdk.Execution;

namespace Games.Poker.Application.Execution;

public sealed class PokerAutoTurnAction
    : IGameAction<PokerAutoTurnCommand, PokerExecutionState, ActionResult>
{
    public GameDecision<PokerExecutionState, ActionResult> Decide(
        GameActionInput<PokerExecutionState, PokerAutoTurnCommand> input)
    {
        if (input.State.Table is not { Status: PokerTableStatus.HandActive })
            return PokerPlayerTurnAction.Reject(input.State, PokerError.NotYourTurn);
        var state = PokerExecutionRules.Clone(input.State);
        var current = state.Seats.FirstOrDefault(seat => seat.Position == state.Table!.CurrentSeat);
        if (current is not { Status: PokerSeatStatus.Seated })
            return PokerPlayerTurnAction.Reject(input.State, PokerError.NotYourTurn);
        var action = PokerDomain.DecideAutoAction(state.Table!, current);
        PokerDomain.Apply(state.Table!, current, action);
        current.HasActedThisRound = true;
        state.Table!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        var autoKind = action.Kind == PokerActionKind.Check ? AutoAction.Check : AutoAction.Fold;
        var resolution = PokerExecutionRules.Resolve(state, input.UtcNow);
        return new(DecisionStatus.Accepted, state,
            resolution.Result with { AutoActorName = current.DisplayName, AutoKind = autoKind },
            [], [], [], resolution.Events, [], CustomEffects: resolution.Effects);
    }
}
