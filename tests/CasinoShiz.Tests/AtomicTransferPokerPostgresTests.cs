using System.Text.Json;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Games.Poker.Application.Execution;
using Games.Poker.Infrastructure.Persistence;
using Games.Transfer.Application.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicTransferPokerPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<BotFrameworkOptions> FrameworkOptions =
        Options.Create(new BotFrameworkOptions { StartingCoins = 100 });

    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Transfer_TwoWalletsInboxLedgerAndOutboxCommitExactlyOnce()
    {
        var executor = Executor(new TransferDescriptor(), new TransferAction(),
            new TransferStateStore(new EconomicsService(new TestConnectionFactory(database.ConnectionString),
                FrameworkOptions, NullLogger<EconomicsService>.Instance)));
        var command = new TransferCommand(1, 2, 10, "sender", "recipient", 50, 2, 52, "transfer-one");

        var first = await executor.ExecuteAsync(new(command), CancellationToken.None);
        var duplicate = await executor.ExecuteAsync(new(command), CancellationToken.None);

        Assert.Equal(first, duplicate);
        Assert.Equal(48, await Balance(1, 10));
        Assert.Equal(150, await Balance(2, 10));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
    }

    [Fact]
    public async Task Poker_CreateJoinAndStartPersistAsAtomicAggregateAndRetryFromInbox()
    {
        var create = Executor(new PokerCreateDescriptor(), new PokerCreateAction(),
            new PokerExecutionStateStore<PokerCreateCommand>(new EconomicsService(
                new TestConnectionFactory(database.ConnectionString), FrameworkOptions,
                NullLogger<EconomicsService>.Instance)));
        var createCommand = new PokerCreateCommand(1, "alice", 20, "poker-create", 100, 1, 2, [new(1, 20)]);
        var created = await create.ExecuteAsync(new(createCommand), CancellationToken.None);
        var duplicate = await create.ExecuteAsync(new(createCommand), CancellationToken.None);
        Assert.Equal(created, duplicate);

        var join = Executor(new PokerJoinDescriptor(), new PokerJoinAction(),
            new PokerExecutionStateStore<PokerJoinCommand>(new EconomicsService(
                new TestConnectionFactory(database.ConnectionString), FrameworkOptions,
                NullLogger<EconomicsService>.Instance)));
        var joinCommand = new PokerJoinCommand(created.InviteCode, 2, "bob", 20, "poker-join", 100, 8,
            [new(1, 20), new(2, 20)]);
        var joined = await join.ExecuteAsync(new(joinCommand), CancellationToken.None);
        Assert.Equal(PokerError.None, joined.Error);

        var start = Executor(new PokerStartDescriptor(), new PokerStartAction(),
            new PokerExecutionStateStore<PokerStartCommand>(new EconomicsService(
                new TestConnectionFactory(database.ConnectionString), FrameworkOptions,
                NullLogger<EconomicsService>.Instance)));
        var started = await start.ExecuteAsync(new(new PokerStartCommand(
            created.InviteCode, 1, "alice", 20, "poker-start", [new(1, 20), new(2, 20)])),
            CancellationToken.None);

        Assert.Equal(PokerError.None, started.Error);
        Assert.Equal(PokerTableStatus.HandActive, started.Snapshot!.Table.Status);
        Assert.NotEmpty(started.Snapshot.Table.DeckState);
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM poker_seats"));
        Assert.Equal(0, await Balance(1, 20));
        Assert.Equal(0, await Balance(2, 20));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
    }

    private IAtomicGameExecutor<TCommand, TState, TResult> Executor<TCommand, TState, TResult>(
        GameExecutionDescriptor<TCommand, TState, TResult> descriptor,
        IGameAction<TCommand, TState, TResult> action,
        IGameStateStore<TCommand, TState> stateStore)
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        var time = new FixedTimeProvider(Now);
        return new AtomicGameExecutor<TCommand, TState, TResult>(
            new PostgresGameExecutionSessionFactory(connections), new PostgresCommandInbox(),
            new PostgresAtomicGameAvailability(new ConfigurationBuilder().Build()),
            new PostgresAtomicEconomics(FrameworkOptions), new PostgresAtomicQuotaStore(),
            new PostgresAtomicPlayerProtection(time),
            new TransactionalEventCollector(new TestEventSerializer()), descriptor, action, stateStore,
            [], time, new GameExecutionTelemetry(NullLogger<GameExecutionTelemetry>.Instance),
            effectHandlers: [new PostgresWalletEconomyEffectHandler()]);
    }

    private Task<int> Balance(long userId, long chatId) => database.ScalarAsync<int>(
        "SELECT coins FROM users WHERE telegram_user_id=@userId AND balance_scope_id=@chatId",
        new { userId, chatId });

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
