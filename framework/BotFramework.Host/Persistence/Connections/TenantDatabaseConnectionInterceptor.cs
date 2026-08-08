using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace BotFramework.Host.Persistence.Connections;

/// <summary>
/// Applies the ambient tenant values after EF Core opens a pooled connection.
/// This keeps the pool key stable while preserving the PostgreSQL session
/// variables used by tenant-bound RLS policies.
/// </summary>
public sealed class TenantDatabaseConnectionInterceptor : DbConnectionInterceptor
{
    public static TenantDatabaseConnectionInterceptor Instance { get; } = new();

    private TenantDatabaseConnectionInterceptor()
    {
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Apply(connection, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default) => Apply(connection, cancellationToken);

    private static Task Apply(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
            throw new InvalidOperationException("Tenant database session settings require an Npgsql connection.");

        return TenantDatabaseScope.ApplyAsync(npgsqlConnection, cancellationToken);
    }
}
