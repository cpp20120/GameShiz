using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Modules.Migrations;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BotFramework.Host.Composition.ServiceDatabases;

public sealed class OwnedDatabaseMigrationRunner(
    INpgsqlConnectionFactory connections,
    IEnumerable<IModuleMigrations> migrations,
    ILogger<OwnedDatabaseMigrationRunner> logger) : IHostedService
{
    private const string EnsureTrackingTable = """
        CREATE TABLE IF NOT EXISTS __module_migrations (
            module_id TEXT NOT NULL,
            migration_id TEXT NOT NULL,
            content_hash TEXT NOT NULL,
            applied_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            PRIMARY KEY (module_id, migration_id)
        );
        """;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        const string acquireLock = "SELECT pg_advisory_lock(hashtextextended('casinoshiz:owned-migrations', 0));";
        const string releaseLock = "SELECT pg_advisory_unlock(hashtextextended('casinoshiz:owned-migrations', 0));";
        await connection.ExecuteAsync(new CommandDefinition(acquireLock, cancellationToken: cancellationToken));

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(EnsureTrackingTable, cancellationToken: cancellationToken));

            foreach (var module in migrations)
                await ApplyAsync(module, cancellationToken);
        }
        finally
        {
            await connection.ExecuteAsync(new CommandDefinition(releaseLock, cancellationToken: CancellationToken.None));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ApplyAsync(IModuleMigrations module, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<(string migration_id, string content_hash)>(new CommandDefinition(
            "SELECT migration_id, content_hash FROM __module_migrations WHERE module_id = @moduleId",
            new { moduleId = module.ModuleId }, cancellationToken: ct));
        var applied = rows.ToDictionary(row => row.migration_id, row => row.content_hash, StringComparer.Ordinal);

        foreach (var migration in module.Migrations)
        {
            if (applied.TryGetValue(migration.Id, out var existingHash))
            {
                if (!string.Equals(existingHash, migration.ContentHash, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Migration {module.ModuleId}:{migration.Id} changed after apply.");
                continue;
            }

            await using var transaction = await connection.BeginTransactionAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition(migration.Sql, transaction: transaction, cancellationToken: ct));
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO __module_migrations (module_id, migration_id, content_hash) VALUES (@moduleId, @migrationId, @contentHash)",
                new { moduleId = module.ModuleId, migrationId = migration.Id, contentHash = migration.ContentHash },
                transaction: transaction, cancellationToken: ct));
            await transaction.CommitAsync(ct);
            logger.LogInformation("Applied owned migration {ModuleId}:{MigrationId}", module.ModuleId, migration.Id);
        }
    }
}
