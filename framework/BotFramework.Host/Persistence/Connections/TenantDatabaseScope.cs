using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using Dapper;
using Npgsql;

namespace BotFramework.Host.Persistence.Connections;

/// <summary>
/// Projects the ambient framework tenant into every PostgreSQL connection.
///
/// Game modules deliberately do not depend on this type. The database boundary
/// owns the session variables so a later schema migration can add RLS or
/// tenant-keyed tables without changing game commands and stores.
/// </summary>
internal static class TenantDatabaseScope
{
    private const string TenantIdSetting = "casinoshiz.tenant_id";
    private const string ScopeIdSetting = "casinoshiz.scope_id";
    private const string PlayerIdSetting = "casinoshiz.player_id";
    private const string ChannelSetting = "casinoshiz.channel";
    private const string BoundSetting = "casinoshiz.tenant_bound";

    public static NpgsqlConnection CreateConnection(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var options = new[]
        {
            StartupOption(TenantIdSetting, TenantId()),
            StartupOption(ScopeIdSetting, ScopeId()),
            StartupOption(PlayerIdSetting, PlayerId()),
            StartupOption(ChannelSetting, Channel()),
            StartupOption(BoundSetting, IsBound() ? "true" : "false"),
        };
        builder.Options = string.Join(' ', new[] { builder.Options, string.Join(' ', options) }
            .Where(static value => !string.IsNullOrWhiteSpace(value)));
        return new NpgsqlConnection(builder.ConnectionString);
    }

    public static async Task ApplyAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var values = CurrentValues();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            SELECT set_config('casinoshiz.tenant_id', @tenantId, false),
                   set_config('casinoshiz.scope_id', @scopeId, false),
                   set_config('casinoshiz.player_id', @playerId, false),
                   set_config('casinoshiz.channel', @channel, false),
                   set_config('casinoshiz.tenant_bound', @bound, false)
            """,
            values,
            cancellationToken: ct));
    }

    private static object CurrentValues()
    {
        var tenant = RequestMetadataContext.TryGetCurrent()?.TenantContext;
        return new
        {
            tenantId = tenant?.TenantId.Value ?? string.Empty,
            scopeId = tenant?.ScopeId.Value ?? string.Empty,
            playerId = tenant?.PlayerId?.Value ?? string.Empty,
            channel = tenant?.Channel.ToString().ToLowerInvariant() ?? string.Empty,
            bound = tenant is null ? "false" : "true",
        };
    }

    private static string TenantId() => RequestMetadataContext.TryGetCurrent()?.TenantContext?.TenantId.Value ?? string.Empty;

    private static string ScopeId() => RequestMetadataContext.TryGetCurrent()?.TenantContext?.ScopeId.Value ?? string.Empty;

    private static string PlayerId() => RequestMetadataContext.TryGetCurrent()?.TenantContext?.PlayerId?.Value ?? string.Empty;

    private static string Channel() => RequestMetadataContext.TryGetCurrent()?.TenantContext?.Channel.ToString().ToLowerInvariant() ?? string.Empty;

    private static bool IsBound() => RequestMetadataContext.TryGetCurrent()?.TenantContext is not null;

    private static string StartupOption(string name, string value) => $"-c {name}={Quote(value)}";

    private static string Quote(string value) => $"'{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal)}'";
}
