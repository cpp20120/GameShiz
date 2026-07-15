using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Admin.Effects;
using BotFramework.Host.Configuration.RuntimeTuning;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Admin.Effects;
using BotFramework.Sdk.Admin.Execution;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AdminEffectExecutorPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RuntimePatchAndAudit_CommitTogether()
    {
        var executor = CreateExecutor([new RuntimeConfigurationPatchEffectHandler()]);

        await executor.ExecuteAsync(
            Envelope(),
            new AdminEffectPlan<bool>(true,
            [
                new RuntimeConfigurationPatchEffect("{\"Games\":{\"horse\":{\"HorseCount\":6}}}"),
            ]),
            CancellationToken.None);

        Assert.Equal(6, await database.ScalarAsync<int>(
            "SELECT (payload #>> '{Games,horse,HorseCount}')::int FROM runtime_tuning WHERE id = 1"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT count(*) FROM admin_audit WHERE action = 'runtime_tuning.test'"));
    }

    [Fact]
    public async Task LaterEffectFailure_RollsBackPatchAndAudit()
    {
        var executor = CreateExecutor(
        [
            new RuntimeConfigurationPatchEffectHandler(),
            new FailingAdminEffectHandler(),
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            Envelope(),
            new AdminEffectPlan<bool>(true,
            [
                new RuntimeConfigurationPatchEffect("{\"Games\":{\"horse\":{\"HorseCount\":6}}}"),
                new FailingAdminEffect(),
            ]),
            CancellationToken.None));

        Assert.Equal("{}", await database.ScalarAsync<string>(
            "SELECT payload::text FROM runtime_tuning WHERE id = 1"));
        Assert.Equal(0, await database.ScalarAsync<int>("SELECT count(*) FROM admin_audit"));
    }

    [Fact]
    public async Task WalletAdjustment_IsIdempotentAndAudited()
    {
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                "INSERT INTO users (telegram_user_id, balance_scope_id, display_name, coins) VALUES (7, 200, 'Alice', 100)");
        }

        var executor = CreateExecutor(
            [new WalletAdjustmentAdminEffectHandler(Options.Create(new BotFrameworkOptions { StartingCoins = 1_000 }))]);
        var effect = new WalletAdjustmentAdminEffect(7, 200, -40, "admin.test", "wallet-op-1");

        var first = await executor.ExecuteAsync(
            Envelope(),
            new AdminEffectPlan<int>(0, [effect], outputs => (int)outputs["balance"]!),
            CancellationToken.None);
        var second = await executor.ExecuteAsync(
            Envelope(),
            new AdminEffectPlan<int>(0, [effect], outputs => (int)outputs["balance"]!),
            CancellationToken.None);

        Assert.Equal(60, first);
        Assert.Equal(60, second);
        Assert.Equal(60, await database.ScalarAsync<int>(
            "SELECT coins FROM users WHERE telegram_user_id = 7 AND balance_scope_id = 200"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT count(*) FROM economics_ledger WHERE operation_id = 'wallet-op-1'"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT count(*) FROM admin_audit WHERE action = 'runtime_tuning.test'"));
    }

    [Fact]
    public async Task WalletAdjustment_FailureRollsBackWalletLedgerAndAudit()
    {
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                "INSERT INTO users (telegram_user_id, balance_scope_id, display_name, coins) VALUES (8, 200, 'Bob', 100)");
        }

        var executor = CreateExecutor(
        [
            new WalletAdjustmentAdminEffectHandler(Options.Create(new BotFrameworkOptions { StartingCoins = 1_000 })),
            new FailingAdminEffectHandler(),
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            Envelope(),
            new AdminEffectPlan<bool>(true,
            [
                new WalletAdjustmentAdminEffect(8, 200, -40, "admin.test", "wallet-op-2"),
                new FailingAdminEffect(),
            ]),
            CancellationToken.None));

        Assert.Equal(100, await database.ScalarAsync<int>(
            "SELECT coins FROM users WHERE telegram_user_id = 8 AND balance_scope_id = 200"));
        Assert.Equal(0, await database.ScalarAsync<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(0, await database.ScalarAsync<int>("SELECT count(*) FROM admin_audit"));
    }

    private AdminEffectExecutor CreateExecutor(IReadOnlyList<IAdminEffectHandler> handlers)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = database.ConnectionString,
            })
            .Build();
        return new AdminEffectExecutor(
            new NpgsqlConnectionFactory(configuration),
            handlers,
            new WalletAtomicExecutionService(new NpgsqlConnectionFactory(configuration), TimeProvider.System));
    }

    private static AdminExecutionEnvelope Envelope() =>
        new(new AdminActor(42, "test-admin"), "runtime_tuning.test", new { source = "test" });

    private sealed record FailingAdminEffect : IAdminEffect;

    private sealed class FailingAdminEffectHandler : AdminEffectHandler<FailingAdminEffect>
    {
        protected override Task ApplyAsync(
            FailingAdminEffect effect,
            IAdminExecutionContext context,
            CancellationToken ct) =>
            throw new InvalidOperationException("Injected admin effect failure.");
    }
}
