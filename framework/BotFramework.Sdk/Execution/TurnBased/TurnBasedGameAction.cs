namespace BotFramework.Sdk.Execution;

/// <summary>
/// Reusable execution policy for ordinary player turns. Games implement only their move rules
/// and mapping of framework rejections to their public result contract.
/// </summary>
public abstract class TurnBasedGameAction<TCommand, TState, TResult, TPlayerId>
    : IGameAction<TCommand, TState, TResult>
    where TCommand : ITurnGameCommand<TPlayerId>
    where TState : ITurnBasedGameState<TPlayerId>
{
    public GameDecision<TState, TResult> Decide(GameActionInput<TState, TCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rejection = ValidateTurn(input);
        if (rejection is not null)
            return Reject(input.State, rejection);

        var decision = DecideTurn(input);
        ValidateDecision(input.State, decision);
        return decision;
    }

    protected abstract GameDecision<TState, TResult> DecideTurn(GameActionInput<TState, TCommand> input);

    protected abstract TResult CreateRejectedResult(TurnRejection<TPlayerId> rejection);

    protected virtual TurnRejection<TPlayerId>? ValidateTurn(GameActionInput<TState, TCommand> input)
    {
        var state = input.State;
        if (input.Command.ExpectedRevision != state.Revision)
            return CreateRejection(TurnRejectionReason.StaleRevision, state);
        if (state.Status != TurnGameStatus.Active)
            return CreateRejection(TurnRejectionReason.GameNotActive, state);
        if (!EqualityComparer<TPlayerId>.Default.Equals(input.Command.PlayerId, state.CurrentPlayerId))
            return CreateRejection(TurnRejectionReason.NotPlayersTurn, state);
        if (state.TurnDeadline is { } deadline && input.UtcNow >= deadline)
            return CreateRejection(TurnRejectionReason.TurnExpired, state);
        return null;
    }

    private GameDecision<TState, TResult> Reject(TState state, TurnRejection<TPlayerId> rejection) =>
        new(
            DecisionStatus.Rejected,
            state,
            CreateRejectedResult(rejection),
            [],
            [],
            [],
            [],
            [],
            rejection.Code);

    private static TurnRejection<TPlayerId> CreateRejection(TurnRejectionReason reason, TState state) =>
        new(reason, state.Revision, state.CurrentPlayerId, state.TurnDeadline);

    private static void ValidateDecision(
        TState currentState,
        GameDecision<TState, TResult> decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Status != DecisionStatus.Accepted)
            return;

        var expectedRevision = checked(currentState.Revision + 1);
        if (decision.NewState.Revision != expectedRevision)
        {
            throw new InvalidOperationException(
                $"An accepted turn must advance state revision from {currentState.Revision} to {expectedRevision}.");
        }
    }
}
