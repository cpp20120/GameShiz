using BotFramework.Host.Persistence.Connections;

namespace BotFramework.Host.Composition.ServiceDatabases;

/// <summary>
/// Readiness check for the database owned by the current process.
/// The connection factory is deliberately local to the service, so this
/// check cannot accidentally validate a different service's database.
/// </summary>
public sealed class PostgresDatabaseHealthCheck(INpgsqlConnectionFactory connections)
    : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            _ = await command.ExecuteScalarAsync(cancellationToken);
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("owned PostgreSQL is reachable");
        }
        catch (Exception exception)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("owned PostgreSQL is not reachable", exception);
        }
    }
}
