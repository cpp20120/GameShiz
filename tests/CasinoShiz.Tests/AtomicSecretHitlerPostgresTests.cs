using System.Text.Json;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Games.SecretHitler.Application.Execution;
using Games.SecretHitler.Domain.Results;
using Games.SecretHitler.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicSecretHitlerPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<BotFrameworkOptions> FrameworkOptions =
        Options.Create(new BotFrameworkOptions { StartingCoins = 100 });

    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateJoinStart_CommitsAggregateWalletInboxAndOutboxExactlyOnce()
    {
        var create = Executor(new ShCreateDescriptor(), new ShCreateAction(),
            new SecretHitlerExecutionStateStore<ShCreateCommand>(new EconomicsService(
                new TestConnectionFactory(database.ConnectionString), FrameworkOptions,
                NullLogger<EconomicsService>.Instance)));
        var createCommand = new ShCreateCommand(1, "p1", -100, 1, "sh-create", 50, [new(1, 1)]);
        var created = await create.ExecuteAsync(new(createCommand), CancellationToken.None);
        var duplicate = await create.ExecuteAsync(new(createCommand), CancellationToken.None);
        Assert.Equal(created, duplicate);
        Assert.Equal(ShError.None, created.Error);

        var wallets = new List<SecretHitlerWalletRef> { new(1, 1) };
        for (var userId = 2; userId <= 5; userId++)
        {
            var join = Executor(new ShJoinDescriptor(), new ShJoinAction(),
                new SecretHitlerExecutionStateStore<ShJoinCommand>(new EconomicsService(
                    new TestConnectionFactory(database.ConnectionString), FrameworkOptions,
                    NullLogger<EconomicsService>.Instance)));
            var expected = wallets.Append(new(userId, userId)).ToArray();
            var joined = await join.ExecuteAsync(new(new ShJoinCommand(created.InviteCode, userId,
                $"p{userId}", -100, userId, $"sh-join-{userId}", 50, expected)), CancellationToken.None);
            Assert.Equal(ShError.None, joined.Error);
            wallets.Add(new(userId, userId));
        }

        var start = Executor(new ShStartDescriptor(), new ShStartAction(),
            new SecretHitlerExecutionStateStore<ShStartCommand>(new EconomicsService(
                new TestConnectionFactory(database.ConnectionString), FrameworkOptions,
                NullLogger<EconomicsService>.Instance)));
        var started = await start.ExecuteAsync(new(new ShStartCommand(created.InviteCode, 1,
            "p1", -100, 1, "sh-start", wallets)), CancellationToken.None);

        Assert.Equal(ShError.None, started.Error);
        Assert.Equal(ShStatus.Active, started.Snapshot!.Game.Status);
        Assert.Equal(ShPhase.Nomination, started.Snapshot.Game.Phase);
        Assert.Equal(5, await Scalar<int>("SELECT count(*) FROM secret_hitler_players"));
        Assert.Equal(250, await Scalar<int>("SELECT pot FROM secret_hitler_games"));
        Assert.Equal(5, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(6, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(6, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        for (var userId = 1; userId <= 5; userId++) Assert.Equal(50, await Balance(userId, userId));
    }

    [Fact]
    public async Task Leave_RefundsWalletClosesGameAndReturnsDuplicateFromInbox()
    {
        var create = Executor(new ShCreateDescriptor(), new ShCreateAction(),
            new SecretHitlerExecutionStateStore<ShCreateCommand>(new EconomicsService(
                new TestConnectionFactory(database.ConnectionString), FrameworkOptions,
                NullLogger<EconomicsService>.Instance)));
        var created = await create.ExecuteAsync(new(new ShCreateCommand(
            7, "p7", -700, 7, "sh-create-leave", 50, [new(7, 7)])), CancellationToken.None);
        var leave = Executor(new ShLeaveDescriptor(), new ShLeaveAction(),
            new SecretHitlerExecutionStateStore<ShLeaveCommand>(new EconomicsService(
                new TestConnectionFactory(database.ConnectionString), FrameworkOptions,
                NullLogger<EconomicsService>.Instance)));
        var command = new ShLeaveCommand(created.InviteCode, 7, "p7", -700, 7,
            "sh-leave", [new(7, 7)]);

        var first = await leave.ExecuteAsync(new(command), CancellationToken.None);
        var duplicate = await leave.ExecuteAsync(new(command), CancellationToken.None);

        Assert.Equal(first, duplicate);
        Assert.True(first.GameClosed);
        Assert.Equal(100, await Balance(7, 7));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM secret_hitler_players"));
        Assert.Equal((int)ShStatus.Closed, await Scalar<int>("SELECT status FROM secret_hitler_games"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
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
