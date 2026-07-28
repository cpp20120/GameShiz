namespace BotFramework.Contracts.Games;

public sealed record GameAvailability(
    long ChatId,
    string GameId,
    bool Enabled,
    GameAvailabilitySource Source,
    string? Reason = null,
    long? ChangedBy = null,
    DateTimeOffset? ChangedAt = null);
