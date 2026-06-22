namespace BotFramework.Host.Events;

public sealed record ProjectionDescriptor(
    string Name,
    string FullName,
    IReadOnlySet<string> SubscribedEventTypes);
