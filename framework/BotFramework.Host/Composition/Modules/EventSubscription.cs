namespace BotFramework.Host.Composition.Modules;

public sealed record EventSubscription(string EventTypePattern, Type SubscriberType);
