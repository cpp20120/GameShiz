using BotFramework.Sdk.Execution;

namespace Games.Poker.Application.Execution;

public sealed class PokerStartAction
    : IGameAction<PokerStartCommand, PokerExecutionState, StartResult>
{
    public GameDecision<PokerExecutionState, StartResult> Decide(
        GameActionInput<PokerExecutionState, PokerStartCommand> input)
    {
        if (input.State.Table is not { } table
            || input.State.Seats.All(seat => seat.UserId != input.Command.ActorUserId))
            return Reject(input.State, PokerError.NoTable);
        if (table.HostUserId != input.Command.ActorUserId) return Reject(input.State, PokerError.NotHost);
        if (table.Status == PokerTableStatus.HandActive) return Reject(input.State, PokerError.HandInProgress);
        if (input.State.Seats.Count(seat => seat.Stack > 0) < 2) return Reject(input.State, PokerError.NeedTwo);

        var state = PokerExecutionRules.Clone(input.State);
        var deck = Deck.BuildShuffled(PokerExecutionRules.ShuffleEntropyNames
            .Select(input.Entropy.GetDouble).ToArray());
        PokerDomain.StartHand(state.Table!, state.Seats, deck, input.UtcNow.ToUnixTimeMilliseconds());
        var active = state.Seats.Count(seat => seat.Status is PokerSeatStatus.Seated or PokerSeatStatus.AllIn);
        return new(DecisionStatus.Accepted, state,
            new(PokerError.None, PokerExecutionRules.Snapshot(state)), [], [], [],
            [new PokerHandStarted(table.InviteCode, active, input.UtcNow.ToUnixTimeMilliseconds())], []);
    }

    private static GameDecision<PokerExecutionState, StartResult> Reject(
        PokerExecutionState state, PokerError error) =>
        new(DecisionStatus.Rejected, state, new(error, null), [], [], [], [], [], error.ToString());
}
