namespace BotFramework.Host.Execution;

using BotFramework.Contracts.Tenancy;
using BotFramework.Contracts.Messaging;

internal sealed record GameEventOutboxItem(
    long Id,
    string TypeName,
    string Payload,
    int Attempts,
    DateTimeOffset CreatedAt);

internal sealed record TenantGameEventOutboxItem(
    long Id,
    string TenantId,
    string ScopeId,
    string? PlayerId,
    string RequestId,
    string CorrelationId,
    string Channel,
    string TypeName,
    string Payload,
    int Attempts,
    DateTimeOffset CreatedAt)
{
    public TenantContext Context => new(
        TenantId: BotFramework.Contracts.Tenancy.TenantId.Create(TenantId),
        ScopeId: BotFramework.Contracts.Tenancy.ScopeId.Create(ScopeId),
        PlayerId: PlayerId is null ? null : BotFramework.Contracts.Tenancy.PlayerId.Create(PlayerId),
        Channel: Enum.TryParse<BotChannel>(Channel, true, out var channel) ? channel : BotChannel.Rest,
        RequestId: BotFramework.Contracts.Tenancy.RequestId.Create(RequestId),
        CorrelationId: BotFramework.Contracts.Tenancy.RequestId.Create(CorrelationId));
}
