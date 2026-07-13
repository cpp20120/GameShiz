namespace BotFramework.Sdk.Execution;

/// <summary>The framework-owned portion of a turn-based aggregate state.</summary>
public interface ITurnBasedGameState<out TPlayerId> : IVersionedGameState
{
    TurnGameStatus Status { get; }

    TPlayerId CurrentPlayerId { get; }

    DateTimeOffset? TurnDeadline { get; }
}
