using System.Collections.Concurrent;
using System.Diagnostics;
using BotFramework.Contracts.Observability;
using BotFramework.Contracts.Caching;
using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Persistence.Connections;
using Dapper;
using Microsoft.Extensions.Caching.Memory;

namespace BotFramework.Host.Tenancy;

/// <summary>Persists first-seen tenants, scopes and transport bindings.</summary>
public sealed class PostgresTenantContextProvisioner(
    INpgsqlConnectionFactory connections,
    IMemoryCache? localCache = null,
    ICacheStore? distributedCache = null)
    : ITenantContextProvisioner
{
    private const string CacheValue = "provisioned";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, Task> inFlight = new(StringComparer.Ordinal);

    public async Task EnsureAsync(TenantContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cacheKey = CacheKey(context);
        if (await IsCachedAsync(cacheKey, cancellationToken))
            return;

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var existing = inFlight.GetOrAdd(cacheKey, completion.Task);
        if (!ReferenceEquals(existing, completion.Task))
        {
            await existing.WaitAsync(cancellationToken);
            return;
        }

        try
        {
            // A different request may have filled the cache after the first
            // check but before this caller acquired the per-key flight slot.
            if (await IsCachedAsync(cacheKey, cancellationToken))
            {
                completion.SetResult(true);
                return;
            }

            await ProvisionAsync(context, cancellationToken);
            SetLocal(cacheKey);
            if (distributedCache is not null)
                await distributedCache.SetStringAsync(cacheKey, CacheValue, CacheTtl, cancellationToken);
            completion.SetResult(true);
        }
        catch (Exception exception)
        {
            completion.SetException(exception);
            throw;
        }
        finally
        {
            inFlight.TryRemove(new KeyValuePair<string, Task>(cacheKey, completion.Task));
        }
    }

    private async Task<bool> IsCachedAsync(string cacheKey, CancellationToken cancellationToken)
    {
        if (localCache?.TryGetValue(cacheKey, out _) == true)
            return true;

        if (distributedCache is null)
            return false;

        var value = await distributedCache.GetStringAsync(cacheKey, cancellationToken);
        if (value is null)
            return false;

        SetLocal(cacheKey);
        return true;
    }

    private void SetLocal(string cacheKey)
    {
        localCache?.Set(cacheKey, CacheValue, CacheTtl);
    }

    private static string CacheKey(TenantContext context) =>
        string.Concat(
            "botframework:tenant-context:v1:",
            (int)context.Channel,
            ':',
            KeyPart(context.TenantId.Value),
            KeyPart(context.ScopeId.Value),
            KeyPart(context.ChannelContainerId),
            KeyPart(context.ChannelTopicId));

    private static string KeyPart(string? value) =>
        value is null ? "-1:" : string.Concat(value.Length, ':', value);

    private async Task ProvisionAsync(TenantContext context, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        BotFrameworkMetrics.TenantProvisioningAttempts.Add(
            1,
            new KeyValuePair<string, object?>("channel", context.Channel.ToString().ToLowerInvariant()));

        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            var tenantKey = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
                """
                INSERT INTO tenants (tenant_id, display_name)
                VALUES (@tenantId, @displayName)
                ON CONFLICT (tenant_id) DO NOTHING
                RETURNING tenant_key
                """,
                new
                {
                    tenantId = context.TenantId.Value,
                    displayName = context.TenantId.Value,
                },
                transaction,
                cancellationToken: cancellationToken));

            if (tenantKey is null)
            {
                tenantKey = await connection.QuerySingleAsync<long>(new CommandDefinition(
                    "SELECT tenant_key FROM tenants WHERE tenant_id = @tenantId",
                    new { tenantId = context.TenantId.Value },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            var scopeKey = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
                """
                INSERT INTO tenant_scopes (tenant_key, scope_id, is_main)
                VALUES (@tenantKey, @scopeId, @isMain)
                ON CONFLICT (tenant_key, scope_id) DO NOTHING
                RETURNING scope_key
                """,
                new
                {
                    tenantKey,
                    scopeId = context.ScopeId.Value,
                    isMain = string.Equals(context.ScopeId.Value, "main", StringComparison.Ordinal),
                },
                transaction,
                cancellationToken: cancellationToken));

            if (scopeKey is null)
            {
                scopeKey = await connection.QuerySingleAsync<long>(new CommandDefinition(
                    "SELECT scope_key FROM tenant_scopes WHERE tenant_key = @tenantKey AND scope_id = @scopeId",
                    new { tenantKey, scopeId = context.ScopeId.Value },
                    transaction,
                    cancellationToken: cancellationToken));
            }

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
                        tenantKey = tenantKey.Value,
                        scopeKey = scopeKey.Value,
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
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
