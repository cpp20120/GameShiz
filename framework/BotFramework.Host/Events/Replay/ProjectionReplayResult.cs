namespace BotFramework.Host.Events.Replay;

public sealed record ProjectionReplayResult(
    string ProjectionName,
    long EventsSeen,
    long EventsApplied);
