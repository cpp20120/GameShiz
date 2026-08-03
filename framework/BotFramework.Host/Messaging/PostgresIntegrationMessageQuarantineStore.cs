using System.Text.Json;
using BotFramework.Host.Persistence.Connections;
using Dapper;

namespace BotFramework.Host.Messaging;

public sealed class PostgresIntegrationMessageQuarantineStore(INpgsqlConnectionFactory connections)
    : IIntegrationMessageQuarantineStore
{
    public async Task QuarantineAsync(IntegrationQuarantineMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        await using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO integration_message_quarantine (
                consumer_name, tenant_id, scope_id, message_id, topic,
                message_type, contract_type, schema_version, payload,
                error_code, error_message)
            VALUES (
                @ConsumerName, @TenantId, @ScopeId, @MessageId, @Topic,
                @MessageType, @ContractType, @SchemaVersion,
                CAST(@Payload AS jsonb), @ErrorCode, @ErrorMessage)
            ON CONFLICT (consumer_name, tenant_id, scope_id, message_id)
            DO UPDATE SET last_seen_at = now(),
                          error_code = EXCLUDED.error_code,
                          error_message = EXCLUDED.error_message,
                          status = 'open'
            """,
            new
            {
                message.ConsumerName,
                TenantId = message.TenantId ?? string.Empty,
                ScopeId = message.ScopeId ?? string.Empty,
                message.MessageId,
                message.Topic,
                message.MessageType,
                message.ContractType,
                message.SchemaVersion,
                Payload = IsJson(message.Payload) ? message.Payload : null,
                message.ErrorCode,
                ErrorMessage = message.ErrorMessage.Length > 4000
                    ? message.ErrorMessage[..4000]
                    : message.ErrorMessage,
            },
            cancellationToken: ct));
        BotFramework.Contracts.Observability.BotFrameworkMetrics.IntegrationMessagesQuarantined.Add(1);
        BotFramework.Contracts.Observability.BotFrameworkMetrics.IntegrationSchemaRejected.Add(1);
    }

    private static bool IsJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind is not JsonValueKind.Undefined;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
