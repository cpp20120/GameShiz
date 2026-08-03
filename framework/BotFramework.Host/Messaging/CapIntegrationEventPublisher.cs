using System.Text.Json;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using DotNetCore.CAP;

namespace BotFramework.Host.Messaging;

/// <summary>
/// CAP-backed adapter for framework integration events. CAP owns durable
/// transport/outbox persistence; modules only depend on the contracts port.
/// </summary>
public sealed class CapIntegrationEventPublisher(
    IIntegrationOutbox outbox,
    IIntegrationMessageRouter router,
    IIntegrationInboxContextAccessor inboxContext,
    ITenantContextAccessor tenantContext,
    IntegrationInboxOptions options) : IIntegrationEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var metadata = RequestMetadataContext.TryGetCurrent();
        var context = tenantContext.Current ?? metadata?.TenantContext;
        var messageId = Guid.NewGuid().ToString("N");
        var correlationId = context?.CorrelationId.ToString()
            ?? metadata?.CorrelationId
            ?? messageId;
        var causationId = metadata?.RequestId ?? messageId;
        var tenantId = context?.TenantId.ToString() ?? metadata?.Tenant?.ToString();
        var scopeId = context?.ScopeId.ToString() ?? metadata?.TypedScope?.ToString();
        var playerId = context?.PlayerId?.ToString() ?? metadata?.Player?.ToString();
        var route = router.Route(
            IntegrationMessageKind.Event,
            integrationEvent.EventType,
            integrationEvent,
            tenantId,
            scopeId,
            playerId);
        var payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions);

        var envelope = new IntegrationEventEnvelope(
            messageId,
            integrationEvent.EventType,
            IntegrationContractNames.Stable(integrationEvent.GetType()),
            SchemaVersion: 1,
            payload,
            integrationEvent.OccurredAt,
            correlationId,
            causationId,
            tenantId,
            scopeId,
            playerId,
            context?.Channel ?? metadata?.Channel ?? BotChannel.Rest,
            route.Topic,
            route.MessageKey);

        return outbox.EnqueueAsync(
            new IntegrationOutboxMessage(
                options.ConsumerName,
                messageId,
                IntegrationMessageKind.Event,
                route.Topic,
                route.MessageKey,
                integrationEvent.EventType,
                envelope.ContractType,
                envelope.SchemaVersion,
                payload,
                JsonSerializer.Serialize(envelope, JsonOptions),
                envelope.OccurredAt,
                envelope.CorrelationId,
                envelope.CausationId,
                envelope.TenantId,
                envelope.ScopeId,
                envelope.PlayerId,
                envelope.Channel),
            inboxContext.Current is { } current
                ? IntegrationTransactionContext.From(current)
                : null,
            ct);
    }

}
