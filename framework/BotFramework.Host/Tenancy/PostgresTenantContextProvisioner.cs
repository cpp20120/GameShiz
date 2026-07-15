using System.Diagnostics;
using BotFramework.Contracts.Observability;
using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Persistence.Connections;
using Dapper;

namespace BotFramework.Host.Tenancy;

/// <summary>Persists first-seen tenants, scopes and transport bindings.</summary>
public sealed class PostgresTenantContextProvisioner(INpgsqlConnectionFactory connections)
    : ITenantContextProvisioner
{
    public async Task EnsureAsync(TenantContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var startedAt = Stopwatch.GetTimestamp();
        BotFrameworkMetrics.TenantProvisioningAttempts.Add(
            1,
            new KeyValuePair<string, object?>("channel", context.Channel.ToString().ToLowerInvariant()));

        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var tenantKey = await connection.QuerySingleAsync<long>(new CommandDefinition(
            """
            INSERT INTO tenants (tenant_id, display_name)
            VALUES (@tenantId, @displayName)
            ON CONFLICT (tenant_id)
            DO UPDATE SET updated_at = now()
            RETURNING tenant_key
            """,
            new
            {
                tenantId = context.TenantId.Value,
                displayName = context.TenantId.Value,
            },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var scopeKey = await connection.QuerySingleAsync<long>(new CommandDefinition(
            """
            INSERT INTO tenant_scopes (tenant_key, scope_id, is_main)
            VALUES (@tenantKey, @scopeId, @isMain)
            ON CONFLICT (tenant_key, scope_id)
            DO UPDATE SET updated_at = now()
            RETURNING scope_key
            """,
            new
            {
                tenantKey,
                scopeId = context.ScopeId.Value,
                isMain = string.Equals(context.ScopeId.Value, "main", StringComparison.Ordinal),
            },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(context.ChannelContainerId))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO channel_bindings (
                    channel, container_id, topic_id, tenant_key, scope_key)
                VALUES (@channel, @containerId, @topicId, @tenantKey, @scopeKey)
                ON CONFLICT (tenant_key, channel, container_id, topic_id)
                DO UPDATE SET tenant_key = EXCLUDED.tenant_key,
                              scope_key = EXCLUDED.scope_key,
                              updated_at = now()
                """,
                new
                {
                    channel = context.Channel.ToString().ToLowerInvariant(),
                    containerId = context.ChannelContainerId,
                    topicId = context.ChannelTopicId,
                    tenantKey,
                    scopeKey,
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            BotFrameworkMetrics.TenantProvisioningFailures.Add(
                1,
                new KeyValuePair<string, object?>("channel", context.Channel.ToString().ToLowerInvariant()));
            throw;
        }
        finally
        {
            BotFrameworkMetrics.TenantProvisioningDuration.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                new KeyValuePair<string, object?>("channel", context.Channel.ToString().ToLowerInvariant()));
        }
    }
}
