namespace BotFramework.Sdk.Execution;

public interface IGameAction<TCommand, TState, TResult>
{
    GameDecision<TState, TResult> Decide(GameActionInput<TState, TCommand> input);
}
