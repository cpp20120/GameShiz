namespace BotFramework.Host.Execution;

using BotFramework.Contracts.Tenancy;
using BotFramework.Contracts.Messaging;

internal sealed record GameScheduleOutboxItem(
    long Id,
    string EffectKind,
    string ScheduleId,
    string? JobKey,
    long? DueAtUnixMilliseconds,
    string Data,
    int Attempts);
