using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Economics.Services;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Economics;
using Dapper;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class WalletAtomicExecutionPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EnsureAndGetBalance_CreatesWalletAndReturnsCurrentSnapshot()
    {
        var wallet = CreateWallet();

        Assert.Equal(0, await wallet.EnsureAndGetBalanceAsync(42, 84, "first", CancellationToken.None));
        await wallet.ApplyBatchAsync(
            42,
            84,
            [new WalletBatchEffect(WalletBatchEffectKind.Credit, 100, "seed")],
            "wallet-seed",
            CancellationToken.None);

        Assert.Equal(100, await wallet.EnsureAndGetBalanceAsync(42, 84, "renamed", CancellationToken.None));
        Assert.Equal("renamed", await database.ScalarAsync<string>(
            "SELECT display_name FROM users WHERE telegram_user_id = 42 AND balance_scope_id = 84"));
    }

    [Fact]
    public async Task ApplyBatch_ConcurrentRetriesPersistOneLedgerAndReturnOneOutcome()
    {
        var wallet = CreateWallet();
        await SeedAsync(wallet, 42, 84, 100);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => wallet.ApplyBatchAsync(
                42,
                84,
                [new WalletBatchEffect(WalletBatchEffectKind.Debit, 10, "pick.bet")],
                "pick-command-42",
                CancellationToken.None));
        var results = await Task.WhenAll(tasks);

        Assert.All(results, result =>
        {
            Assert.True(result.Applied);
            Assert.False(result.Rejected);
            Assert.Equal(90, result.NewBalance);
        });
        Assert.Equal(90, await database.ScalarAsync<int>(
            "SELECT coins FROM users WHERE telegram_user_id = 42 AND balance_scope_id = 84"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT count(*) FROM economics_ledger WHERE operation_id = 'pick-command-42:0'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT count(*) FROM wallet_operations WHERE operation_id = 'pick-command-42' AND status = 'completed' AND applied"));
    }

    [Fact]
    public async Task ApplyBatch_RejectedOperationIsIdempotentAndRecordsFinalBalance()
    {
        var wallet = CreateWallet();
        await SeedAsync(wallet, 42, 84, 5);
        var effects = new[] { new WalletBatchEffect(WalletBatchEffectKind.Debit, 10, "pick.bet") };

        var first = await wallet.ApplyBatchAsync(42, 84, effects, "reject-command-42", CancellationToken.None);
        var second = await wallet.ApplyBatchAsync(42, 84, effects, "reject-command-42", CancellationToken.None);

        Assert.Equal(first, second);
        Assert.False(first.Applied);
        Assert.True(first.Rejected);
        Assert.Equal(5, first.NewBalance);
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT count(*) FROM economics_ledger WHERE operation_id LIKE 'reject-command-42:%'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT count(*) FROM wallet_operations WHERE operation_id = 'reject-command-42' AND status = 'rejected' AND NOT applied AND balance_after = 5"));
    }

    [Fact]
    public async Task WalletOperationMigration_BackfillsLegacyLedgerForSafeRetries()
    {
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync("""
                INSERT INTO users (telegram_user_id, balance_scope_id, display_name, coins, version)
                VALUES (42, 84, 'user', 90, 2);
                INSERT INTO economics_ledger
                    (telegram_user_id, balance_scope_id, delta, balance_after, reason, operation_id)
                VALUES
                    (42, 84, -10, 90, 'pick.bet', 'legacy-command:0'),
                    (42, 84, 20, 110, 'pick.win', 'legacy-command:1');
                UPDATE users SET coins = 110, version = 2
                WHERE telegram_user_id = 42 AND balance_scope_id = 84;
                DELETE FROM wallet_operations;
                """);
            var migration = Assert.Single(
                new BotFramework.Host.Composition.Migrations.FrameworkMigrations().Migrations,
                item => item.Id == "039_wallet_operation_results");
            await connection.ExecuteAsync(migration.Sql);
        }

        var result = await CreateWallet().ApplyBatchAsync(
            42,
            84,
            [new WalletBatchEffect(WalletBatchEffectKind.Debit, 10, "pick.bet")],
            "legacy-command",
            CancellationToken.None);

        Assert.True(result.Applied);
        Assert.False(result.Rejected);
        Assert.Equal(110, result.NewBalance);
        Assert.Equal(110, await database.ScalarAsync<int>(
            "SELECT coins FROM users WHERE telegram_user_id = 42 AND balance_scope_id = 84"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT count(*) FROM economics_ledger WHERE operation_id LIKE 'legacy-command:%'"));
    }

    private WalletAtomicExecutionService CreateWallet() =>
        new(new TestConnectionFactory(database.ConnectionString), TimeProvider.System);

    private static async Task SeedAsync(
        IWalletAtomicExecutionService wallet,
        long userId,
        long scopeId,
        int balance)
    {
        await wallet.EnsureUserAsync(userId, scopeId, "user", CancellationToken.None);
        await wallet.ApplyBatchAsync(
            userId,
            scopeId,
            [new WalletBatchEffect(WalletBatchEffectKind.Credit, balance, "seed")],
            $"seed-{userId}-{scopeId}",
            CancellationToken.None);
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
