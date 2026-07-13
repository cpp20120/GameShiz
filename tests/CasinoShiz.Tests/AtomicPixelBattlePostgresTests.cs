using System.Text.Json;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Dapper;
using Games.PixelBattle.Application.Execution;
using Games.PixelBattle.Contracts;
using Games.PixelBattle.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicPixelBattlePostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<BotFrameworkOptions> FrameworkOptions =
        Options.Create(new BotFrameworkOptions { StartingCoins = 100 });

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync("""
            INSERT INTO users (telegram_user_id,balance_scope_id,display_name,coins)
            VALUES (42,0,'pixel user',100)
            """);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UpdateAndDuplicate_CommitTileInboxAndOutboxExactlyOnce()
    {
        var executor = Executor();
        var command = new PixelBattleCommand(42, 12, "#EF4444", "pixel-one");

        var first = await executor.ExecuteAsync(new(command), CancellationToken.None);
        var duplicate = await executor.ExecuteAsync(new(command), CancellationToken.None);

        Assert.Equal(first, duplicate);
        Assert.Equal(PixelUpdateStatus.Updated, first.Status);
        Assert.Equal("#EF4444", await Scalar<string>("SELECT color FROM pixelbattle_tiles WHERE index=12"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM pixelbattle_tiles"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
    }

    [Fact]
    public async Task ConcurrentTileCommands_PreserveEveryCommandAndMonotonicVersions()
    {
        var executor = Executor();
        var colors = PixelBattleConstants.Colors.Take(16).ToArray();
        var tasks = colors.Select((color, index) => executor.ExecuteAsync(new(new PixelBattleCommand(
            42, index % 4, color, $"pixel-concurrent-{index}")), CancellationToken.None));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, result => Assert.Equal(PixelUpdateStatus.Updated, result.Status));
        Assert.Equal(16, results.Select(result => result.Update!.Versionstamp).Distinct().Count());
        Assert.Equal(4, await Scalar<int>("SELECT count(*) FROM pixelbattle_tiles"));
        Assert.Equal(16, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(16, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
    }

    private IAtomicGameExecutor<PixelBattleCommand, PixelBattleExecutionState, PixelUpdateResult> Executor()
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        var time = new FixedTimeProvider(Now);
        return new AtomicGameExecutor<PixelBattleCommand, PixelBattleExecutionState, PixelUpdateResult>(
            new PostgresGameExecutionSessionFactory(connections), new PostgresCommandInbox(),
            new PostgresAtomicGameAvailability(new ConfigurationBuilder().Build()),
            new PostgresAtomicEconomics(FrameworkOptions), new PostgresAtomicQuotaStore(),
            new PostgresAtomicPlayerProtection(time),
            new TransactionalEventCollector(new TestEventSerializer()),
            new PixelBattleDescriptor(), new PixelBattleAction(), new PixelBattleExecutionStateStore(),
            [], time, new GameExecutionTelemetry(NullLogger<GameExecutionTelemetry>.Instance));
    }

    private Task<T> Scalar<T>(string sql) => database.ScalarAsync<T>(sql);

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

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class TestEventSerializer : IEventSerializer
    {
        public string Serialize(IDomainEvent ev) => JsonSerializer.Serialize(ev, ev.GetType());
        public IDomainEvent? Deserialize(string eventType, string payloadJson) => throw new NotSupportedException();
    }
}
