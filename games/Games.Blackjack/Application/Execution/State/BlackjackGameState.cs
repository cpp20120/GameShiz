using BotFramework.Sdk.Execution;

namespace Games.Blackjack.Application.Execution;

public sealed record BlackjackGameState(
    long Revision,
    TurnGameStatus Status,
    long CurrentPlayerId,
    DateTimeOffset? TurnDeadline,
    string DisplayName,
    BlackjackHandState? Hand) : ITurnBasedGameState<long>;
