using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Host.Execution;

internal sealed record TenantGameScheduleOutboxItem(
    long Id,
    string TenantId,
    string ScopeId,
    string? PlayerId,
    string RequestId,
    string CorrelationId,
    string Channel,
    string EffectKind,
    string ScheduleId,
    string? JobKey,
    long? DueAtUnixMilliseconds,
    string Data,
    int Attempts)
{
    public TenantContext Context => new(
        TenantId: BotFramework.Contracts.Tenancy.TenantId.Create(TenantId),
        ScopeId: BotFramework.Contracts.Tenancy.ScopeId.Create(ScopeId),
        PlayerId: PlayerId is null ? null : BotFramework.Contracts.Tenancy.PlayerId.Create(PlayerId),
        Channel: Enum.TryParse<BotChannel>(Channel, true, out var channel) ? channel : BotChannel.Rest,
        RequestId: BotFramework.Contracts.Tenancy.RequestId.Create(RequestId),
        CorrelationId: BotFramework.Contracts.Tenancy.RequestId.Create(CorrelationId));
}
