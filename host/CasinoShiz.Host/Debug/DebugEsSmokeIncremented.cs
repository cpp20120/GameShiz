using BotFramework.Sdk;

namespace CasinoShiz.Host.Debug;

public sealed record DebugEsSmokeIncremented(
    string StreamId,
    int Count,
    long UserId,
    long ChatId,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "debug.es_smoke_incremented";
}
