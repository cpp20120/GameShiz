using System.Text.Json;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Games.Bowling.Application.Execution;
using Games.Bowling.Infrastructure.Persistence;
using Games.Football.Application.Execution;
using Games.Football.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicBowlingFootballPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Bowling_PlaceAndRollCommitWalletQuotaStateInboxAndEventsAtomically()
    {
        var place = CreateExecutor(
            new BowlingPlaceDescriptor(), new BowlingPlaceBetAction(), new BowlingBetStateStore());
        var envelope = new GameExecutionEnvelope<BowlingPlaceBetCommand>(
            new(42, "bowler", 84, 50, "bowling-place", 100, null));

        var first = await place.ExecuteAsync(envelope, CancellationToken.None);
        var duplicate = await place.ExecuteAsync(envelope, CancellationToken.None);
        Assert.Equal(first, duplicate);

        var roll = CreateExecutor(
            new BowlingRollDescriptor(), new BowlingRollAction(), new BowlingBetStateStore());
        var result = await roll.ExecuteAsync(
            new(new(42, "bowler", 84, 6, "bowling-roll", 0)), CancellationToken.None);

        Assert.Equal(100, result.Payout);
        Assert.Equal(150, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(1, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM bowling_bets"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM mini_game_sessions"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
    }

    [Fact]
    public async Task Football_PlaceAndThrowCommitWalletQuotaStateInboxAndEventsAtomically()
    {
        var place = CreateExecutor(
            new FootballPlaceDescriptor(), new FootballPlaceBetAction(), new FootballBetStateStore());
        var envelope = new GameExecutionEnvelope<FootballPlaceBetCommand>(
            new(42, "player", 84, 50, "football-place", 100, null));

        var first = await place.ExecuteAsync(envelope, CancellationToken.None);
        var duplicate = await place.ExecuteAsync(envelope, CancellationToken.None);
        Assert.Equal(first, duplicate);

        var settle = CreateExecutor(
            new FootballThrowDescriptor(), new FootballThrowAction(), new FootballBetStateStore());
        var result = await settle.ExecuteAsync(
            new(new(42, "player", 84, 5, "football-throw", 0)), CancellationToken.None);

        Assert.Equal(100, result.Payout);
        Assert.Equal(150, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(1, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM football_bets"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM mini_game_sessions"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
    }

    private IAtomicGameExecutor<TCommand, TState, TResult> CreateExecutor<TCommand, TState, TResult>(
        GameExecutionDescriptor<TCommand, TState, TResult> descriptor,
        IGameAction<TCommand, TState, TResult> action,
        IGameStateStore<TCommand, TState> stateStore)
    {
        var time = new FixedTimeProvider(Now);
        return new AtomicGameExecutor<TCommand, TState, TResult>(
            new PostgresGameExecutionSessionFactory(new TestConnectionFactory(database.ConnectionString)),
            new PostgresCommandInbox(),
            new PostgresAtomicGameAvailability(new ConfigurationBuilder().Build()),
            new PostgresAtomicEconomics(Options.Create(new BotFrameworkOptions { StartingCoins = 100 })),
            new PostgresAtomicQuotaStore(),
            new PostgresAtomicPlayerProtection(time),
            new TransactionalEventCollector(new TestEventSerializer()),
            descriptor, action, stateStore, [], time,
            new GameExecutionTelemetry(NullLogger<GameExecutionTelemetry>.Instance));
    }

    private Task<T> Scalar<T>(string sql) => database.ScalarAsync<T>(sql);

    private abstract class BowlingDescriptor<TCommand, TResult>
        : GameExecutionDescriptor<TCommand, BowlingBetState, TResult>
    {
        public override string GameId => "bowling";
        public override string AggregateId(TCommand command) => "84:42";
        public override long ChatId(TCommand command) => 84;
        public override string DisplayName(TCommand command) => "bowler";
        public override WalletIdentity Wallet(TCommand command) => new(42, 84);
        public override IReadOnlyList<QuotaIdentity> Quotas(TCommand command, DateTimeOffset utcNow) =>
            [new(BowlingPlaceBetAction.DailyRollQuota, GameId, 42, 84,
                DateOnly.FromDateTime(utcNow.UtcDateTime), 10)];
    }

    private sealed class BowlingPlaceDescriptor : BowlingDescriptor<BowlingPlaceBetCommand, BowlingBetResult>
    {
        public override string CommandId(BowlingPlaceBetCommand command) => command.CommandId;
    }

    private sealed class BowlingRollDescriptor : BowlingDescriptor<BowlingRollCommand, BowlingRollResult>
    {
        public override string CommandId(BowlingRollCommand command) => command.CommandId;
        public override IReadOnlyList<string> EntropyNames => [BowlingRollAction.RedeemDropEntropy];
    }

    private abstract class FootballDescriptor<TCommand, TResult>
        : GameExecutionDescriptor<TCommand, FootballBetState, TResult>
    {
        public override string GameId => "football";
        public override string AggregateId(TCommand command) => "84:42";
        public override long ChatId(TCommand command) => 84;
        public override string DisplayName(TCommand command) => "player";
        public override WalletIdentity Wallet(TCommand command) => new(42, 84);
        public override IReadOnlyList<QuotaIdentity> Quotas(TCommand command, DateTimeOffset utcNow) =>
            [new(FootballPlaceBetAction.DailyRollQuota, GameId, 42, 84,
                DateOnly.FromDateTime(utcNow.UtcDateTime), 10)];
    }

    private sealed class FootballPlaceDescriptor : FootballDescriptor<FootballPlaceBetCommand, FootballBetResult>
    {
        public override string CommandId(FootballPlaceBetCommand command) => command.CommandId;
    }

    private sealed class FootballThrowDescriptor : FootballDescriptor<FootballThrowCommand, FootballThrowResult>
    {
        public override string CommandId(FootballThrowCommand command) => command.CommandId;
        public override IReadOnlyList<string> EntropyNames => [FootballThrowAction.RedeemDropEntropy];
    }

    private sealed class TestConnectionFactory(string connectionString) : INpgsqlConnectionFactory
    {
        public NpgsqlConnection Create() => new(connectionString);

        public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
        {
            var connection = Create();
            await connection.OpenAsync(ct).ConfigureAwait(false);
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
