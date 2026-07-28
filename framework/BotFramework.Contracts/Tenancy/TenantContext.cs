using BotFramework.Contracts.Messaging;

namespace BotFramework.Contracts.Tenancy;

/// <summary>
/// Complete tenant boundary for one inbound operation.
/// </summary>
public sealed record TenantContext(
    TenantId TenantId,
    ScopeId ScopeId,
    PlayerId? PlayerId,
    BotChannel Channel,
    RequestId RequestId,
    RequestId CorrelationId)
{
    /// <summary>Platform container used to provision a channel binding.</summary>
    public string? ChannelContainerId { get; init; }

    /// <summary>Platform topic/thread identifier used to provision a binding.</summary>
    public string? ChannelTopicId { get; init; }

    public bool HasPlayer => PlayerId.HasValue;

    public static TenantContext Create(
        TenantId tenantId,
        ScopeId scopeId,
        PlayerId? playerId,
        BotChannel channel,
        RequestId? requestId = null,
        RequestId? correlationId = null) =>
        new(
            tenantId,
            scopeId,
            playerId,
            channel,
            requestId ?? RequestId.New(),
            correlationId ?? requestId ?? RequestId.New());
}
