using BotFramework.Contracts.Observability;
using BotFramework.Host.Persistence.Connections;
using Dapper;
using Microsoft.Extensions.Options;

namespace BotFramework.Host.Composition.ServiceDatabases;

public sealed class PostgresServiceOwnershipValidator(
    INpgsqlConnectionFactory connections,
    IOptions<ServiceOwnershipOptions> options) : IServiceOwnershipValidator
{
    public async Task<ServiceOwnershipReport> ValidateAsync(CancellationToken ct = default)
    {
        var configured = options.Value;
        if (!configured.Enforce)
            return new(true, "", "", configured.Schema, []);
        if (string.IsNullOrWhiteSpace(configured.Schema))
            throw new InvalidOperationException("ServiceOwnership:Schema is required when ownership checks are enforced.");

        await using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<OwnershipRow>(new CommandDefinition(
            """
            SELECT current_database() AS Database,
                   current_user AS User,
                   current_schema() AS CurrentSchema,
                   EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = @Schema) AS SchemaExists,
                   has_schema_privilege(current_user, @Schema, 'USAGE') AS HasUsage,
                   has_schema_privilege(current_user, @Schema, 'CREATE') AS HasCreate,
                   r.rolsuper AS IsSuperuser,
                   r.rolbypassrls AS BypassesRls
            FROM pg_roles AS r
            WHERE r.rolname = current_user
            """,
            new { configured.Schema },
            cancellationToken: ct));

        if (row is null)
            throw new InvalidOperationException("PostgreSQL did not return the current service role.");

        var violations = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured.ExpectedDatabase)
            && !string.Equals(row.Database, configured.ExpectedDatabase, StringComparison.Ordinal))
            violations.Add($"database '{row.Database}' does not match expected '{configured.ExpectedDatabase}'");
        if (!row.SchemaExists)
            violations.Add($"schema '{configured.Schema}' does not exist");
        if (!row.HasUsage)
            violations.Add($"role '{row.User}' has no USAGE privilege on schema '{configured.Schema}'");
        if (configured.RequireSchemaCreate && !row.HasCreate)
            violations.Add($"role '{row.User}' has no CREATE privilege on schema '{configured.Schema}'");
        if (configured.RequireNonSuperuser && row.IsSuperuser)
            violations.Add($"role '{row.User}' is a PostgreSQL superuser");
        if (configured.RequireNonSuperuser && row.BypassesRls)
            violations.Add($"role '{row.User}' has BYPASSRLS");

        if (violations.Count != 0)
            BotFrameworkMetrics.ServiceOwnershipViolations.Add(violations.Count);

        return new(
            violations.Count == 0,
            row.Database,
            row.User,
            configured.Schema,
            violations);
    }

    private sealed record OwnershipRow(
        string Database,
        string User,
        string CurrentSchema,
        bool SchemaExists,
        bool HasUsage,
        bool HasCreate,
        bool IsSuperuser,
        bool BypassesRls);
}
