using System.Reflection;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Host.Messaging;

/// <summary>
/// Dispatches an integration envelope to typed handlers inside one service.
/// The transport is deliberately invisible to handlers, while tenant and
/// request metadata are restored before the handler is invoked.
/// </summary>
public sealed partial class IntegrationEventDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<IntegrationEventDispatcher> logger)
{
    public async Task DispatchAsync(IntegrationEventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var scope = scopeFactory.CreateScope();
        Type eventType;
        object integrationEvent;
        try
        {
            var parsed = (scope.ServiceProvider.GetService<IntegrationMessageSchemaValidator>()
                          ?? new IntegrationMessageSchemaValidator())
                .DeserializeEvent(envelope);
            eventType = parsed.Type;
            integrationEvent = parsed.Message;
        }
        catch (IntegrationSchemaValidationException exception)
        {
            await QuarantineAsync(scope.ServiceProvider, envelope, exception, ct);
            return;
        }

        var tenant = CreateTenantContext(envelope);
        using var tenantScope = tenant is null
            ? null
            : scope.ServiceProvider.GetService<ITenantContextAccessor>()?.Push(tenant);
        using var metadataScope = RequestMetadataContext.Push(CreateRequestMetadata(envelope, tenant));

        var handlerServiceType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
        var handlers = scope.ServiceProvider.GetServices(handlerServiceType).ToArray();
        if (handlers.Length == 0)
        {
            LogNoHandler(envelope.EventType, eventType.FullName ?? eventType.Name);
            return;
        }

        var handleMethod = handlerServiceType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))
            ?? throw new InvalidOperationException($"Integration handler method is missing for '{eventType.FullName}'.");

        var inbox = scope.ServiceProvider.GetService<IIntegrationInbox>();
        if (inbox is null)
        {
            await InvokeHandlersAsync(handlers, handleMethod, integrationEvent, envelope.EventType, ct);
            return;
        }

        await inbox.ExecuteOnceAsync(
            ToInboxMessage(envelope),
            (_, handlerCt) => InvokeHandlersAsync(handlers, handleMethod, integrationEvent, envelope.EventType, handlerCt),
            ct);
    }

    private static async Task InvokeHandlersAsync(
        object?[] handlers,
        MethodInfo handleMethod,
        object integrationEvent,
        string eventType,
        CancellationToken ct)
    {
        foreach (var handler in handlers)
        {
            if (handler is null)
                throw new InvalidOperationException($"Integration handler is null for '{eventType}'.");

            try
            {
                var task = (Task?)handleMethod.Invoke(handler, [integrationEvent, ct])
                    ?? throw new InvalidOperationException($"Integration handler returned null for '{eventType}'.");
                await task;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }
    }

    private static IntegrationInboxMessage ToInboxMessage(IntegrationEventEnvelope envelope) =>
        new(
            envelope.MessageId,
            envelope.EventType,
            envelope.ContractType,
            envelope.SchemaVersion,
            envelope.Payload,
            envelope.OccurredAt,
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.TenantId,
            envelope.ScopeId,
            envelope.PlayerId,
            envelope.Channel,
            envelope.Topic,
            envelope.MessageKey);

    private async Task QuarantineAsync(
        IServiceProvider services,
        IntegrationEventEnvelope envelope,
        IntegrationSchemaValidationException exception,
        CancellationToken ct)
    {
        var store = services.GetService<IIntegrationMessageQuarantineStore>();
        if (store is null)
        {
            LogSchemaRejected(envelope.EventType, exception.Code);
            return;
        }

        var options = services.GetService<IntegrationInboxOptions>();
        await store.QuarantineAsync(
            new IntegrationQuarantineMessage(
                options?.ConsumerName ?? "unknown",
                envelope.TenantId,
                envelope.ScopeId,
                envelope.MessageId,
                envelope.Topic ?? IntegrationMessagingTopics.Events,
                envelope.EventType,
                envelope.ContractType,
                envelope.SchemaVersion,
                envelope.Payload,
                exception.Code,
                exception.Message),
            ct);
        LogQuarantined(envelope.EventType, exception.Code);
    }

    private static TenantContext? CreateTenantContext(IntegrationEventEnvelope envelope)
    {
        if (!TenantId.TryParse(envelope.TenantId, null, out var tenantId)
            || !ScopeId.TryParse(envelope.ScopeId, null, out var scopeId))
            return null;

        var playerId = PlayerId.TryParse(envelope.PlayerId, null, out var parsedPlayer)
            ? parsedPlayer
            : (PlayerId?)null;
        var requestId = RequestId.TryParse(envelope.MessageId, null, out var parsedRequest)
            ? parsedRequest
            : RequestId.New();
        var correlationId = RequestId.TryParse(envelope.CorrelationId, null, out var parsedCorrelation)
            ? parsedCorrelation
            : requestId;

        return TenantContext.Create(tenantId, scopeId, playerId, envelope.Channel, requestId, correlationId);
    }

    private static RequestMetadata CreateRequestMetadata(
        IntegrationEventEnvelope envelope,
        TenantContext? tenant)
    {
        var requestId = envelope.MessageId;
        var correlationId = string.IsNullOrWhiteSpace(envelope.CorrelationId)
            ? requestId
            : envelope.CorrelationId;
        return new RequestMetadata(
            requestId,
            correlationId,
            $"integration:{envelope.EventType}",
            envelope.PlayerId,
            envelope.ScopeId,
            "en",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["message_id"] = envelope.MessageId,
                ["causation_id"] = envelope.CausationId,
                ["event_type"] = envelope.EventType,
                ["topic"] = envelope.Topic ?? IntegrationMessagingTopics.Events,
                ["message_key"] = envelope.MessageKey ?? string.Empty,
            })
        {
            Tenant = tenant?.TenantId,
            TypedScope = tenant?.ScopeId,
            Player = tenant?.PlayerId,
            TenantContext = tenant,
            Channel = envelope.Channel,
        };
    }

    [LoggerMessage(LogLevel.Debug, "integration_event.no_handler event={EventType} contract={ContractType}")]
    private partial void LogNoHandler(string eventType, string contractType);

    [LoggerMessage(LogLevel.Warning, "integration_event.schema_rejected event={EventType} code={Code}")]
    private partial void LogSchemaRejected(string eventType, string code);

    [LoggerMessage(LogLevel.Warning, "integration_event.quarantined event={EventType} code={Code}")]
    private partial void LogQuarantined(string eventType, string code);
}
