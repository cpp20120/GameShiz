using Dapper;

namespace BotFramework.Host.Execution;

internal sealed class TransactionalEventCollector(IEventSerializer serializer)
    : ITransactionalEventCollector
{
    public async Task AppendAsync(
        string commandId,
        IReadOnlyList<IDomainEvent> events,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        if (events.Count == 0) return;

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
}
