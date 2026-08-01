using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Economics.Options;
using BotFramework.Host.Economics.Services;
using BotFramework.Host.Persistence.Connections;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class WalletTenantIsolationPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly IOptions<BotFrameworkOptions> Options =
        Microsoft.Extensions.Options.Options.Create(new BotFrameworkOptions { StartingCoins = 100 });

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SamePrivateTelegramChatInDifferentBotsHasIndependentWallets()
    {
        var wallet = new EconomicsService(
            new TestConnectionFactory(database.ConnectionString),
            Options,
            NullLogger<EconomicsService>.Instance,
            new WalletScopeResolver());

        using (BotTenant("telegram:dm:42"))
        {
            await wallet.EnsureUserAsync(42, 42, "user", CancellationToken.None);
            await wallet.CreditAsync(42, 42, 25, "seed", CancellationToken.None);
        }

        using (BotTenant("telegram:bot2:dm:42"))
        {
            await wallet.EnsureUserAsync(42, 42, "user", CancellationToken.None);
            Assert.True(await wallet.TryDebitAsync(42, 42, 10, "bet", CancellationToken.None));
            Assert.Equal(115, await wallet.GetBalanceAsync(42, 42, CancellationToken.None));
        }

        using (BotTenant("telegram:dm:42"))
        {
            Assert.Equal(125, await wallet.GetBalanceAsync(42, 42, CancellationToken.None));
            await wallet.CreditAsync(42, 42, 5, "credit", CancellationToken.None);
        }

        using (BotTenant("telegram:bot2:dm:42"))
        {
            Assert.Equal(115, await wallet.GetBalanceAsync(42, 42, CancellationToken.None));
        }

        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT count(*) FROM wallet_scope_aliases WHERE tenant_id = 'telegram:bot2:dm:42'"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT count(*) FROM users WHERE telegram_user_id = 42"));
    }

    private static IDisposable BotTenant(string tenantId)
    {
        var context = TenantContext.Create(
            TenantId.Create(tenantId),
            ScopeId.Create("main"),
            PlayerId.Create("42"),
            BotChannel.Telegram);
        return RequestMetadataContext.Push(RequestMetadata.FromTenantContext(context, "test"));
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
