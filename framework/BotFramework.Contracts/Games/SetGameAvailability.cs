namespace BotFramework.Contracts.Games;

public sealed record SetGameAvailability(
    long ChatId,
    string GameId,
    bool Enabled,
    string Reason,
    long ActorId,
    string ActorName);
