using BotFramework.Sdk.Execution;

namespace Games.Poker.Application.Execution;

public sealed class PokerSetMessageAction
    : IGameAction<PokerSetMessageCommand, PokerExecutionState, bool>
{
    public GameDecision<PokerExecutionState, bool> Decide(
        GameActionInput<PokerExecutionState, PokerSetMessageCommand> input)
    {
        if (input.State.Table is null)
            return new(DecisionStatus.Rejected, input.State, false, [], [], [], [], [], "no_table");
        var state = PokerExecutionRules.Clone(input.State);
        state.Table!.StateMessageId = input.Command.MessageId;
        return new(DecisionStatus.Accepted, state, true, [], [], [], [], []);
    }
}
