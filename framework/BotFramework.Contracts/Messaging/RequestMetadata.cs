namespace BotFramework.Contracts.Messaging;

using BotFramework.Contracts.Tenancy;
using System.Collections.ObjectModel;

public sealed record RequestMetadata(
    string RequestId,
    string CorrelationId,
    string ClientId,
    string? UserId,
    string? ScopeId,
    string Culture,
    IReadOnlyDictionary<string, string> Baggage)
{
    private static readonly IReadOnlyDictionary<string, string> EmptyBaggage =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Typed tenant boundary for new SDK consumers.</summary>
    public TenantId? Tenant { get; init; }

    /// <summary>Typed scope for new SDK consumers.</summary>
    public ScopeId? TypedScope { get; init; }

    /// <summary>Typed player identity for new SDK consumers.</summary>
    public PlayerId? Player { get; init; }

    /// <summary>Complete canonical context propagated through in-process and gRPC calls.</summary>
    public TenantContext? TenantContext { get; init; }

    public RequestId TypedRequestId => BotFramework.Contracts.Tenancy.RequestId.Create(RequestId);

    public RequestId TypedCorrelationId => BotFramework.Contracts.Tenancy.RequestId.Create(CorrelationId);

    /// <summary>
    /// Logical source/delivery channel. System is used when no user-facing
    /// transport exists; inbound adapters must set their concrete channel.
    /// </summary>
    public BotChannel Channel { get; init; } = BotChannel.System;

    public static RequestMetadata Create(
        string clientId,
        string? userId = null,
        string? scopeId = null,
        string culture = "en",
        BotChannel channel = BotChannel.System)
    {
        var requestId = Guid.NewGuid().ToString("N");
        return new RequestMetadata(
            requestId,
            requestId,
            clientId,
            userId,
            scopeId,
            culture,
            new Dictionary<string, string>(StringComparer.Ordinal))
        {
            Channel = channel,
        };
    }

    public static RequestMetadata System(
        string clientId = "system",
        string culture = "en") =>
        Create(clientId, culture: culture, channel: BotChannel.System);

    public static RequestMetadata FromTenantContext(
        TenantContext context,
        string clientId,
        string culture = "en",
        IReadOnlyDictionary<string, string>? baggage = null) =>
        new(
            context.RequestId.ToString(),
            context.CorrelationId.ToString(),
            clientId,
            context.PlayerId?.ToString(),
            context.ScopeId.ToString(),
            culture,
            baggage ?? EmptyBaggage)
        {
            Tenant = context.TenantId,
            TypedScope = context.ScopeId,
            Player = context.PlayerId,
            Channel = context.Channel,
            TenantContext = context,
        };
}
