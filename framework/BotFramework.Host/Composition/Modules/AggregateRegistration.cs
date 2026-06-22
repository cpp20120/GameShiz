using BotFramework.Sdk;

namespace BotFramework.Host.Composition.Modules;

public sealed record AggregateRegistration(Type AggregateType, PersistenceStrategy Strategy);
