namespace BotFramework.Host.Composition;

public sealed record EventSubscription(string EventTypePattern, Type SubscriberType);
