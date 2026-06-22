namespace BotFramework.Host.Events.Replay;

public sealed record ProjectionDescriptor(
    string Name,
    string FullName,
    IReadOnlySet<string> SubscribedEventTypes);
