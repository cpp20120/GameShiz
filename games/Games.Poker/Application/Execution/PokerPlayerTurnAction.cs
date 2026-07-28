using BotFramework.Sdk.Execution;

namespace Games.Poker.Application.Execution;

public sealed class PokerPlayerTurnAction
    : IGameAction<PokerPlayerTurnCommand, PokerExecutionState, ActionResult>
{
    public GameDecision<PokerExecutionState, ActionResult> Decide(
        GameActionInput<PokerExecutionState, PokerPlayerTurnCommand> input)
    {
        if (input.State.Table is not { Status: PokerTableStatus.HandActive } table)
            return Reject(input.State, PokerError.NotYourTurn);
        var state = PokerExecutionRules.Clone(input.State);
        var live = state.Seats.FirstOrDefault(seat => seat.UserId == input.Command.ActorUserId);
        if (live is null) return Reject(input.State, PokerError.NoTable);
        if (live.Position != table.CurrentSeat || live.Status != PokerSeatStatus.Seated)
            return Reject(input.State, PokerError.NotYourTurn);
        var action = PokerAction.FromVerb(input.Command.Verb, input.Command.Amount);
        if (action is null) return Reject(input.State, PokerError.InvalidAction);
        var validation = PokerDomain.Validate(state.Table!, live, action.Value);
        if (validation != ValidationResult.Ok)
            return Reject(input.State, PokerExecutionRules.MapValidation(validation));
        PokerDomain.Apply(state.Table!, live, action.Value);
        live.HasActedThisRound = true;
        state.Table!.LastActionAt = input.UtcNow.ToUnixTimeMilliseconds();
        var resolution = PokerExecutionRules.Resolve(state, input.UtcNow);
        return new(DecisionStatus.Accepted, state, resolution.Result, [], [], [], resolution.Events, [],
            CustomEffects: resolution.Effects);
    }

    internal static GameDecision<PokerExecutionState, ActionResult> Reject(
        PokerExecutionState state, PokerError error) =>
        new(DecisionStatus.Rejected, state,
            new(error, null, HandTransition.None, null, null, null), [], [], [], [], [], error.ToString());
}
