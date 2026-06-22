namespace BotFramework.Host.Events;

public sealed record ProjectionReplayResult(
    string ProjectionName,
    long EventsSeen,
    long EventsApplied);
