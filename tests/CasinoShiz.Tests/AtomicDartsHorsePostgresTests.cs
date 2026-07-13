using System.Text.Json;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Games.Darts.Application.Execution;
using Games.Darts.Infrastructure.Persistence;
using Games.Horse.Application.Execution;
using Games.Horse.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicDartsHorsePostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task QueuedDarts_BetQuotaRoundSettlementInboxAndOutboxAreAtomic()
    {
        var place = Executor(
            new DartsDescriptor<DartsPlaceBetCommand, DartsBetResult>(command => command.CommandId,
                command => command.UserId, command => command.ChatId, command => command.DisplayName),
            new DartsPlaceBetAction(), new DartsPlaceBetStateStore());
        var placeCommand = new DartsPlaceBetCommand(42, "dart", 84, 20, 100, 7, "darts-place", 100, null);
        var first = await place.ExecuteAsync(new(placeCommand), CancellationToken.None);
        var duplicate = await place.ExecuteAsync(new(placeCommand), CancellationToken.None);
        Assert.Equal(first, duplicate);

        var store = new DartsRoundStore(new TestConnectionFactory(database.ConnectionString));
        Assert.True(await store.TryMarkAwaitingOutcomeAsync(7, 9001, CancellationToken.None));
        var resolve = Executor(
            new DartsDescriptor<DartsResolveRoundCommand, DartsThrowResult>(command => command.CommandId,
                command => command.UserId, command => command.ChatId, command => command.DisplayName,
                [DartsResolveRoundAction.RedeemDropEntropy]),
            new DartsResolveRoundAction(), new DartsResolveRoundStateStore());
        var result = await resolve.ExecuteAsync(new(new DartsResolveRoundCommand(
            7, 42, "dart", 84, 9001, 6, "darts-resolve", 0)), CancellationToken.None);

        Assert.Equal(DartsThrowOutcome.Thrown, result.Outcome);
        Assert.Equal(120, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(1, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM darts_rounds"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
    }

    [Fact]
    public async Task Horse_MultiWalletRacePayoutAndResultsCommitExactlyOnce()
    {
        var betDescriptor = new HorsePlaceBetDescriptor();
        var betExecutor = Executor(betDescriptor, new HorsePlaceBetAction(), new HorsePlaceBetStateStore());
        var raceDate = "07-13-2026";
        var commands = new[]
        {
            new HorsePlaceBetCommand(42, "a", 84, 1, 20, raceDate, Guid.NewGuid(), "horse-bet-a", 1),
            new HorsePlaceBetCommand(43, "b", 84, 1, 20, raceDate, Guid.NewGuid(), "horse-bet-b", 1),
        };
        foreach (var command in commands)
            Assert.Equal(HorseError.None, (await betExecutor.ExecuteAsync(new(command), CancellationToken.None)).Error);

        var readStore = new HorseBetStore(new TestConnectionFactory(database.ConnectionString));
        var bets = await readStore.ListByRaceDateAsync(raceDate, CancellationToken.None);
        var run = Executor(new HorseRunDescriptor(), new HorseRunAction(), new HorseRunStateStore(),
            [new PostgresWalletEconomyEffectHandler()]);
        var commandRun = new HorseRunCommand(99, HorseRunKind.Global, 0, 0, raceDate, bets,
            "horse-run", 1, 2, true);
        var result = await run.ExecuteAsync(new(commandRun), CancellationToken.None);
        var duplicate = await run.ExecuteAsync(new(commandRun), CancellationToken.None);

        Assert.Equal(result.Error, duplicate.Error);
        Assert.Equal(result.Winner, duplicate.Winner);
        Assert.Equal(result.RaceDate, duplicate.RaceDate);
        Assert.Equal(result.Transactions, duplicate.Transactions);
        Assert.Equal(result.Participants, duplicate.Participants);
        Assert.Equal(result.BetScopeIds, duplicate.BetScopeIds);
        Assert.Equal(result.GifBytes, duplicate.GifBytes);
        Assert.Equal(200, await Scalar<int>("SELECT sum(coins) FROM users"));
        Assert.Equal(4, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM horse_bets"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM horse_results"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
    }

    private IAtomicGameExecutor<TCommand, TState, TResult> Executor<TCommand, TState, TResult>(
        GameExecutionDescriptor<TCommand, TState, TResult> descriptor,
        IGameAction<TCommand, TState, TResult> action,
        IGameStateStore<TCommand, TState> stateStore,
        IEnumerable<IGameEffectHandler>? handlers = null)
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        var time = new FixedTimeProvider(Now);
        return new AtomicGameExecutor<TCommand, TState, TResult>(
            new PostgresGameExecutionSessionFactory(connections), new PostgresCommandInbox(),
            new PostgresAtomicGameAvailability(new ConfigurationBuilder().Build()),
            new PostgresAtomicEconomics(Options.Create(new BotFrameworkOptions { StartingCoins = 100 })),
            new PostgresAtomicQuotaStore(), new PostgresAtomicPlayerProtection(time),
            new TransactionalEventCollector(new TestEventSerializer()), descriptor, action, stateStore,
            [], time, new GameExecutionTelemetry(NullLogger<GameExecutionTelemetry>.Instance),
            effectHandlers: handlers);
    }

    private Task<T> Scalar<T>(string sql) => database.ScalarAsync<T>(sql);

    private sealed class DartsDescriptor<TCommand, TResult>(
        Func<TCommand, string> commandId,
        Func<TCommand, long> userId,
        Func<TCommand, long> chatId,
        Func<TCommand, string> displayName,
        IReadOnlyList<string>? entropy = null)
        : GameExecutionDescriptor<TCommand, DartsQueuedState, TResult>
    {
        public override string GameId => MiniGameIds.Darts;
        public override IReadOnlyList<string> EntropyNames => entropy ?? [];
        public override string CommandId(TCommand command) => commandId(command);
        public override string AggregateId(TCommand command) => $"{chatId(command)}:{userId(command)}";
        public override long ChatId(TCommand command) => chatId(command);
        public override string DisplayName(TCommand command) => displayName(command);
        public override WalletIdentity Wallet(TCommand command) => new(userId(command), chatId(command));
        public override IReadOnlyList<QuotaIdentity> Quotas(TCommand command, DateTimeOffset utcNow) =>
            [new(DartsPlaceBetAction.DailyRollQuota, MiniGameIds.Darts, userId(command), chatId(command),
                DateOnly.FromDateTime(utcNow.Date), 10)];
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
