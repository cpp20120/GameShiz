using System.Reflection;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Host.Messaging;

public sealed partial class IntegrationCommandDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<IntegrationCommandDispatcher> logger)
{
    public async Task DispatchAsync(IntegrationCommandEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var scope = scopeFactory.CreateScope();
        Type commandType;
        object command;
        try
        {
            var parsed = (scope.ServiceProvider.GetService<IntegrationMessageSchemaValidator>()
                          ?? new IntegrationMessageSchemaValidator())
                .DeserializeCommand(envelope);
            commandType = parsed.Type;
            command = parsed.Message;
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

        var handlerServiceType = typeof(IIntegrationCommandHandler<>).MakeGenericType(commandType);
        var handlers = scope.ServiceProvider.GetServices(handlerServiceType).ToArray();
        if (handlers.Length == 0)
        {
            LogNoHandler(envelope.CommandType, commandType.FullName ?? commandType.Name);
            return;
        }

        var handleMethod = handlerServiceType.GetMethod(nameof(IIntegrationCommandHandler<IIntegrationCommand>.HandleAsync))
            ?? throw new InvalidOperationException($"Integration command handler method is missing for '{commandType.FullName}'.");

        var inbox = scope.ServiceProvider.GetService<IIntegrationInbox>();
        if (inbox is null)
        {
            await InvokeHandlersAsync(handlers, handleMethod, command, envelope.CommandType, ct);
            return;
        }

        await inbox.ExecuteOnceAsync(
            ToInboxMessage(envelope),
            (_, handlerCt) => InvokeHandlersAsync(handlers, handleMethod, command, envelope.CommandType, handlerCt),
            ct);
    }

    private static async Task InvokeHandlersAsync(
        object?[] handlers,
        MethodInfo handleMethod,
        object command,
        string commandType,
        CancellationToken ct)
    {
        foreach (var handler in handlers)
        {
            if (handler is null)
                throw new InvalidOperationException($"Integration command handler is null for '{commandType}'.");

            try
            {
                var task = (Task?)handleMethod.Invoke(handler, [command, ct])
                    ?? throw new InvalidOperationException($"Integration command handler returned null for '{commandType}'.");
                await task;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }
    }

    private static IntegrationInboxMessage ToInboxMessage(IntegrationCommandEnvelope envelope) =>
        new(
            envelope.MessageId,
            envelope.CommandType,
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
        IntegrationCommandEnvelope envelope,
        IntegrationSchemaValidationException exception,
        CancellationToken ct)
    {
        var store = services.GetService<IIntegrationMessageQuarantineStore>();
        if (store is null)
        {
            LogSchemaRejected(envelope.CommandType, exception.Code);
            return;
        }

        var options = services.GetService<IntegrationInboxOptions>();
        await store.QuarantineAsync(
            new IntegrationQuarantineMessage(
                options?.ConsumerName ?? "unknown",
                envelope.TenantId,
                envelope.ScopeId,
                envelope.MessageId,
                envelope.Topic ?? IntegrationMessagingTopics.Commands,
                envelope.CommandType,
                envelope.ContractType,
                envelope.SchemaVersion,
                envelope.Payload,
                exception.Code,
                exception.Message),
            ct);
        LogQuarantined(envelope.CommandType, exception.Code);
    }

    private static TenantContext? CreateTenantContext(IntegrationCommandEnvelope envelope)
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
        IntegrationCommandEnvelope envelope,
        TenantContext? tenant)
    {
        var requestId = envelope.MessageId;
        var correlationId = string.IsNullOrWhiteSpace(envelope.CorrelationId)
            ? requestId
            : envelope.CorrelationId;
        return new RequestMetadata(
            requestId,
            correlationId,
            $"integration:{envelope.CommandType}",
            envelope.PlayerId,
            envelope.ScopeId,
            "en",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["message_id"] = envelope.MessageId,
                ["causation_id"] = envelope.CausationId,
                ["command_type"] = envelope.CommandType,
                ["topic"] = envelope.Topic ?? IntegrationMessagingTopics.Commands,
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

    [LoggerMessage(LogLevel.Debug, "integration_command.no_handler command={CommandType} contract={ContractType}")]
    private partial void LogNoHandler(string commandType, string contractType);

    [LoggerMessage(LogLevel.Warning, "integration_command.schema_rejected command={CommandType} code={Code}")]
    private partial void LogSchemaRejected(string commandType, string code);

    [LoggerMessage(LogLevel.Warning, "integration_command.quarantined command={CommandType} code={Code}")]
    private partial void LogQuarantined(string commandType, string code);
}
