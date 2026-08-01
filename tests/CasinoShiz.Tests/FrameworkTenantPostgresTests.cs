using BotFramework.Contracts.Economics;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.RateLimiting;
using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Host.RateLimiting;
using BotFramework.Host.Tenancy;
using BotFramework.Host.Economics;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class FrameworkTenantPostgresTests(AtomicPostgresFixture database)
{
    [Fact]
    public async Task ConnectionFactory_ProjectsAndRefreshesAmbientTenantSession()
    {
        await database.ResetAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = database.ConnectionString,
            })
            .Build();
        var factory = new NpgsqlConnectionFactory(configuration);
        var tenant = TenantContext.Create(
            TenantId.Create("framework-db-scope"),
            ScopeId.Create("main"),
            PlayerId.Create("player"),
            BotChannel.Telegram);

        using (RequestMetadataContext.Push(RequestMetadata.FromTenantContext(tenant, "test")))
        {
            await using var connection = await factory.OpenAsync(CancellationToken.None);
            var values = await connection.QuerySingleAsync<(string TenantId, string ScopeId, string PlayerId, string Channel, string Bound)>(
                """
                SELECT current_setting('casinoshiz.tenant_id') AS TenantId,
                       current_setting('casinoshiz.scope_id') AS ScopeId,
                       current_setting('casinoshiz.player_id') AS PlayerId,
                       current_setting('casinoshiz.channel') AS Channel,
                       current_setting('casinoshiz.tenant_bound') AS Bound
                """);

            Assert.Equal(("framework-db-scope", "main", "player", "telegram", "true"), values);
        }

        await using var unbound = await factory.OpenAsync(CancellationToken.None);
        var unboundValues = await unbound.QuerySingleAsync<(string TenantId, string ScopeId, string Bound)>(
            """
            SELECT current_setting('casinoshiz.tenant_id') AS TenantId,
                   current_setting('casinoshiz.scope_id') AS ScopeId,
                   current_setting('casinoshiz.tenant_bound') AS Bound
            """);
        Assert.Equal((string.Empty, string.Empty, "false"), unboundValues);
    }

    [Fact]
    public async Task Provisioner_CreatesIndependentTenantScopesAndBindings()
    {
        await database.ResetAsync();
        var provisioner = new PostgresTenantContextProvisioner(new TestConnectionFactory(database.ConnectionString));
        var scope = ScopeId.Create("main");
        var first = TenantContext.Create(
            TenantId.Create("framework-test-a"),
            scope,
            PlayerId.Create("same-player"),
            BotChannel.Telegram,
            RequestId.New(),
            RequestId.New()) with
        {
            ChannelContainerId = "1001",
        };
        var second = first with
        {
            TenantId = TenantId.Create("framework-test-b"),
            ChannelContainerId = "1002",
        };

        await provisioner.EnsureAsync(first);
        await provisioner.EnsureAsync(second);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var rows = (await Dapper.SqlMapper.QueryAsync<(string TenantId, string ScopeId, string ContainerId)>(
            connection,
            """
            SELECT t.tenant_id AS TenantId, s.scope_id AS ScopeId, b.container_id AS ContainerId
            FROM channel_bindings b
            JOIN tenants t ON t.tenant_key = b.tenant_key
            JOIN tenant_scopes s ON s.tenant_key = b.tenant_key AND s.scope_key = b.scope_key
            WHERE t.tenant_id LIKE 'framework-test-%'
            ORDER BY t.tenant_id
            """)).ToArray();

        Assert.Equal(2, rows.Length);
        Assert.Equal(("framework-test-a", "main", "1001"), rows[0]);
        Assert.Equal(("framework-test-b", "main", "1002"), rows[1]);
    }

    [Fact]
    public async Task PolicyProvider_AppliesRouteBeforeChannelOverride()
    {
        await database.ResetAsync();
        var factory = new TestConnectionFactory(database.ConnectionString);
        var provisioner = new PostgresTenantContextProvisioner(factory);
        var tenant = TenantId.Create("framework-policy-test");
        await provisioner.EnsureAsync(TenantContext.Create(
            tenant,
            ScopeId.Create("main"),
            PlayerId.Create("player"),
            BotChannel.Telegram));

        var admin = new PostgresRateLimitPolicyProvider(
            factory,
            NullLogger<PostgresRateLimitPolicyProvider>.Instance);
        await admin.UpsertAsync(new RateLimitPolicyOverride(
            tenant,
            BotChannel.Telegram,
            null,
            RateLimitDimension.TenantPlayer,
            new RateLimitPolicy(3, 0)));
        await admin.UpsertAsync(new RateLimitPolicyOverride(
            tenant,
            null,
            "command:hot",
            RateLimitDimension.TenantPlayer,
            new RateLimitPolicy(1, 0)));

        var provider = admin;
        var deployment = new RateLimitPolicySet(
            new RateLimitPolicy(100, 0),
            new RateLimitPolicy(100, 0),
            new RateLimitPolicy(100, 0),
            new RateLimitPolicy(100, 0),
            new RateLimitPolicy(100, 0),
            "deployment-1");
        var hot = await provider.ResolveAsync(new RateLimitRequest(
            tenant,
            PlayerId.Create("player"),
            BotChannel.Telegram,
            "command:hot"), deployment);
        var cold = await provider.ResolveAsync(new RateLimitRequest(
            tenant,
            PlayerId.Create("player"),
            BotChannel.Telegram,
            "command:cold"), deployment);

        Assert.Equal(1, hot.Player.Capacity);
        Assert.Equal(3, cold.Player.Capacity);
        Assert.StartsWith("deployment-1:tenant-", hot.Version, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TenantWalletRead_IsolatesSamePlayerAndScopeAcrossTenants()
    {
        await database.ResetAsync();
        var factory = new TestConnectionFactory(database.ConnectionString);
        var provisioner = new PostgresTenantContextProvisioner(factory);
        var scope = ScopeId.Create("main");
        var player = PlayerId.Create("same-player");
        var first = TenantContext.Create(
            TenantId.Create("wallet-test-a"), scope, player, BotChannel.Rest);
        var second = first with { TenantId = TenantId.Create("wallet-test-b") };

        await provisioner.EnsureAsync(first);
        await provisioner.EnsureAsync(second);
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                INSERT INTO tenant_wallets (tenant_key, scope_key, player_id, coins)
                SELECT t.tenant_key, s.scope_key, @playerId, @coins
                FROM tenants t
                JOIN tenant_scopes s ON s.tenant_key = t.tenant_key AND s.scope_id = @scopeId
                WHERE t.tenant_id = @tenantId
                """,
                new { tenantId = first.TenantId.Value, scopeId = scope.Value, playerId = player.Value, coins = 17 });
            await connection.ExecuteAsync(
                """
                INSERT INTO tenant_wallets (tenant_key, scope_key, player_id, coins)
                SELECT t.tenant_key, s.scope_key, @playerId, @coins
                FROM tenants t
                JOIN tenant_scopes s ON s.tenant_key = t.tenant_key AND s.scope_id = @scopeId
                WHERE t.tenant_id = @tenantId
                """,
                new { tenantId = second.TenantId.Value, scopeId = scope.Value, playerId = player.Value, coins = 29 });
        }

        var read = new PostgresTenantWalletReadService(factory);
        Assert.Equal(17, (await read.GetAsync(first))!.Balance);
        Assert.Equal(29, (await read.GetAsync(second))!.Balance);
    }

    [Fact]
    public async Task TenantExecutionRecords_IsolateSameAggregateCommandAndSchedule()
    {
        await database.ResetAsync();
        var factory = new TestConnectionFactory(database.ConnectionString);
        var provisioner = new PostgresTenantContextProvisioner(factory);
        var scope = ScopeId.Create("main");
        var first = TenantContext.Create(
            TenantId.Create("execution-test-a"), scope, PlayerId.Create("same-player"), BotChannel.Rest);
        var second = first with { TenantId = TenantId.Create("execution-test-b") };
        await provisioner.EnsureAsync(first);
        await provisioner.EnsureAsync(second);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        foreach (var tenant in new[] { first.TenantId.Value, second.TenantId.Value })
        {
            var parameters = new
            {
                tenantId = tenant,
                scopeId = scope.Value,
                gameId = "coin-flip",
                aggregateId = "same-aggregate",
                commandId = "same-command",
                requestId = $"request-{tenant}",
            };
            await connection.ExecuteAsync(
                """
                INSERT INTO tenant_aggregate_states (
                    tenant_key, scope_key, game_id, aggregate_id, state_type, version, state)
                SELECT t.tenant_key, s.scope_key, @gameId, @aggregateId, 'state', 0, '{}'::jsonb
                FROM tenants t
                JOIN tenant_scopes s ON s.tenant_key = t.tenant_key AND s.scope_id = @scopeId
                WHERE t.tenant_id = @tenantId;

                INSERT INTO tenant_event_outbox (
                    tenant_key, scope_key, command_id, event_index, event_type, type_name,
                    payload, occurred_at, request_id, correlation_id, channel)
                SELECT t.tenant_key, s.scope_key, @commandId, 0, 'state.changed', 'System.Object',
                       '{}'::jsonb, now(), @requestId, @requestId, 'rest'
                FROM tenants t
                JOIN tenant_scopes s ON s.tenant_key = t.tenant_key AND s.scope_id = @scopeId
                WHERE t.tenant_id = @tenantId;

                INSERT INTO tenant_schedule_outbox (
                    tenant_key, scope_key, command_id, effect_index, game_id, schedule_id,
                    effect_kind, due_at, data, request_id, correlation_id, channel)
                SELECT t.tenant_key, s.scope_key, @commandId, 0, @gameId, 'same-schedule',
                       'schedule', now(), '{}'::jsonb, @requestId, @requestId, 'rest'
                FROM tenants t
                JOIN tenant_scopes s ON s.tenant_key = t.tenant_key AND s.scope_id = @scopeId
                WHERE t.tenant_id = @tenantId;
                """,
                parameters);
        }

        var stateCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT count(*)
            FROM tenant_aggregate_states a
            JOIN tenants t ON t.tenant_key = a.tenant_key
            WHERE t.tenant_id = @tenantId AND a.scope_key = (
                SELECT scope_key FROM tenant_scopes s
                WHERE s.tenant_key = a.tenant_key AND s.scope_id = @scopeId)
            """,
            new { tenantId = first.TenantId.Value, scopeId = scope.Value });
        var secondStateCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM tenant_aggregate_states a JOIN tenants t ON t.tenant_key = a.tenant_key WHERE t.tenant_id = @tenantId",
            new { tenantId = second.TenantId.Value });
        Assert.Equal(1, stateCount);
        Assert.Equal(1, secondStateCount);
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM tenant_event_outbox e JOIN tenants t ON t.tenant_key = e.tenant_key WHERE t.tenant_id = @tenantId",
            new { tenantId = first.TenantId.Value }));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM tenant_schedule_outbox s JOIN tenants t ON t.tenant_key = s.tenant_key WHERE t.tenant_id = @tenantId",
            new { tenantId = second.TenantId.Value }));
    }

    [Fact]
    public async Task LegacyGameTables_AreStampedAndFilteredByFrameworkTenantContext()
    {
        await database.ResetAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = database.ConnectionString,
            })
            .Build();
        var factory = new NpgsqlConnectionFactory(configuration);
        var provisioner = new PostgresTenantContextProvisioner(
            new TestConnectionFactory(database.ConnectionString));
        var first = TenantContext.Create(
            TenantId.Create("legacy-game-test-a"),
            ScopeId.Create("main"),
            PlayerId.Create("same-player"),
            BotChannel.Telegram);
        var second = first with { TenantId = TenantId.Create("legacy-game-test-b") };
        await provisioner.EnsureAsync(first);
        await provisioner.EnsureAsync(second);

        await using (var admin = new NpgsqlConnection(database.ConnectionString))
        {
            await admin.OpenAsync();
            await admin.ExecuteAsync("""
                DO $role$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'tenant_boundary_test') THEN
                        CREATE ROLE tenant_boundary_test NOLOGIN;
                    END IF;
                END
                $role$;
                GRANT SELECT, INSERT, UPDATE, DELETE ON darts_rounds TO tenant_boundary_test;
                GRANT USAGE, SELECT ON SEQUENCE darts_rounds_id_seq TO tenant_boundary_test;
                GRANT SELECT ON tenants, tenant_scopes TO tenant_boundary_test;
                """);
        }

        using (RequestMetadataContext.Push(RequestMetadata.FromTenantContext(first, "test")))
        {
            await using var connection = await factory.OpenAsync(CancellationToken.None);
            await connection.ExecuteAsync("SET ROLE tenant_boundary_test");
            await connection.ExecuteAsync("""
                INSERT INTO darts_rounds (user_id, chat_id, amount, status, reply_to_message_id, channel)
                VALUES (925337014, 925337014, 10, 0, 0, 'telegram')
                """);

            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM darts_rounds WHERE user_id = 925337014"));
            Assert.Equal("legacy-game-test-a", await connection.ExecuteScalarAsync<string>("""
                SELECT t.tenant_id
                FROM darts_rounds d
                JOIN tenants t ON t.tenant_key = d.tenant_key
                WHERE d.user_id = 925337014
                """));
        }

        using (RequestMetadataContext.Push(RequestMetadata.FromTenantContext(second, "test")))
        {
            await using var connection = await factory.OpenAsync(CancellationToken.None);
            await connection.ExecuteAsync("SET ROLE tenant_boundary_test");
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM darts_rounds WHERE user_id = 925337014"));

            await connection.ExecuteAsync("""
                INSERT INTO darts_rounds (user_id, chat_id, amount, status, reply_to_message_id, channel)
                VALUES (925337014, 925337014, 20, 0, 0, 'telegram')
                """);
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM darts_rounds WHERE user_id = 925337014"));
        }

        await using (var unbound = await factory.OpenAsync(CancellationToken.None))
        {
            await unbound.ExecuteAsync("SET ROLE tenant_boundary_test");
            Assert.Equal(2, await unbound.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM darts_rounds WHERE user_id = 925337014"));
        }
    }

    private sealed class TestConnectionFactory(string connectionString) : INpgsqlConnectionFactory
    {
        public NpgsqlConnection Create() => new(connectionString);

        public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
        {
            var connection = Create();
            await connection.OpenAsync(ct);
            return connection;
        }
    }
}
