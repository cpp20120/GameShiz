using BotFramework.Sdk;

namespace BotFramework.Host.Composition;

public sealed record AggregateRegistration(Type AggregateType, PersistenceStrategy Strategy);
