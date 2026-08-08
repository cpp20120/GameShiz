using BotFramework.Host.Persistence.Connections;
using Games.Meta.Infrastructure.Persistence;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class MetaStorePostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetOrCreateActiveSeasonAsync_ConcurrentFirstRequests_CreateOneSeason()
    {
        var stores = Enumerable.Range(0, 12)
            .Select(_ => new MetaStore(new TestConnectionFactory(database.ConnectionString), new FakeRuntimeTuning()))
            .ToArray();

        var seasons = await Task.WhenAll(stores.Select(store => store.GetOrCreateActiveSeasonAsync(CancellationToken.None)));

        Assert.Single(seasons.Select(season => season.Id).Distinct());
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT count(*) FROM meta_seasons WHERE status = 'active'"));
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
