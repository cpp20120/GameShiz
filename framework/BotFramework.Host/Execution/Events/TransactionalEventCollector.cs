using Dapper;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Host.Execution;

internal sealed class TransactionalEventCollector(IEventSerializer serializer)
    : ITransactionalEventCollector
{
    public async Task AppendAsync(
        string commandId,
        IReadOnlyList<IDomainEvent> events,
        IGameExecutionSession session,
        CancellationToken ct)
        => await AppendAsync(commandId, events, session, null, ct).ConfigureAwait(false);

    public async Task AppendAsync(
        string commandId,
        IReadOnlyList<IDomainEvent> events,
        IGameExecutionSession session,
        TenantContext? tenantContext,
        CancellationToken ct)
    {
        if (events.Count == 0) return;

        if (tenantContext is null)
        {
            await AppendLegacyAsync(commandId, events, session, ct).ConfigureAwait(false);
            return;
        }

        const string tenantSql = """
            INSERT INTO tenant_event_outbox (
                tenant_key,
                scope_key,
                command_id,
                event_index,
                event_type,
                type_name,
                payload,
                occurred_at,
                request_id,
                correlation_id,
                channel,
                player_id)
            SELECT t.tenant_key,
                   s.scope_key,
                   @commandId,
                   batch.event_index,
                   batch.event_type,
                   batch.type_name,
                   CAST(batch.payload AS jsonb),
                   to_timestamp(batch.occurred_at / 1000.0),
                   @requestId,
                   @correlationId,
                   @channel,
                   @playerId
            FROM tenants t
            JOIN tenant_scopes s ON s.tenant_key = t.tenant_key AND s.scope_id = @scopeId
            CROSS JOIN unnest(
                CAST(@eventIndexes AS integer[]),
                CAST(@eventTypes AS text[]),
                CAST(@typeNames AS text[]),
                CAST(@payloads AS text[]),
                CAST(@occurredAts AS bigint[]))
                AS batch(event_index, event_type, type_name, payload, occurred_at)
            WHERE t.tenant_id = @tenantId
            ON CONFLICT (tenant_key, scope_key, command_id, event_index) DO NOTHING
            """;

        await session.Connection.ExecuteAsync(new CommandDefinition(
            tenantSql,
            Parameters(commandId, events, tenantContext),
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    private async Task AppendLegacyAsync(
        string commandId,
        IReadOnlyList<IDomainEvent> events,
        IGameExecutionSession session,
        CancellationToken ct)
    {

        const string sql = """
            INSERT INTO game_event_outbox (
                command_id,
                event_index,
                event_type,
                type_name,
                payload,
                occurred_at)
            SELECT @commandId,
                   batch.event_index,
                   batch.event_type,
                   batch.type_name,
                   CAST(batch.payload AS jsonb),
                   to_timestamp(batch.occurred_at / 1000.0)
            FROM unnest(
                CAST(@eventIndexes AS integer[]),
                CAST(@eventTypes AS text[]),
                CAST(@typeNames AS text[]),
                CAST(@payloads AS text[]),
                CAST(@occurredAts AS bigint[]))
                AS batch(event_index, event_type, type_name, payload, occurred_at)
            ON CONFLICT (command_id, event_index) DO NOTHING
            """;

        await session.Connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                commandId,
                eventIndexes = Enumerable.Range(0, events.Count).ToArray(),
                eventTypes = events.Select(domainEvent => domainEvent.EventType).ToArray(),
                typeNames = events.Select(domainEvent => domainEvent.GetType().AssemblyQualifiedName
                    ?? throw new InvalidOperationException("Domain event type has no assembly-qualified name.")).ToArray(),
                payloads = events.Select(serializer.Serialize).ToArray(),
                occurredAts = events.Select(domainEvent => domainEvent.OccurredAt).ToArray(),
            },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);
    }

    private object Parameters(string commandId, IReadOnlyList<IDomainEvent> events, TenantContext tenantContext) =>
        new
        {
            commandId,
            eventIndexes = Enumerable.Range(0, events.Count).ToArray(),
            eventTypes = events.Select(domainEvent => domainEvent.EventType).ToArray(),
            typeNames = events.Select(domainEvent => domainEvent.GetType().AssemblyQualifiedName
                ?? throw new InvalidOperationException("Domain event type has no assembly-qualified name.")).ToArray(),
            payloads = events.Select(serializer.Serialize).ToArray(),
            occurredAts = events.Select(domainEvent => domainEvent.OccurredAt).ToArray(),
            tenantId = tenantContext.TenantId.Value,
            scopeId = tenantContext.ScopeId.Value,
            requestId = tenantContext.RequestId.Value,
            correlationId = tenantContext.CorrelationId.Value,
            channel = tenantContext.Channel.ToString().ToLowerInvariant(),
            playerId = tenantContext.PlayerId?.Value,
        };
}
