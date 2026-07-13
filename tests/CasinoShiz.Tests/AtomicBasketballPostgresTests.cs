using System.Text.Json;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Games.Basketball.Application.Execution;
using Games.Basketball.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicBasketballPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PlaceBet_CommitsWalletQuotaStateSessionInboxAndEventTogether()
    {
        var executor = CreatePlaceExecutor();
        var envelope = new GameExecutionEnvelope<BasketballPlaceBetCommand>(Place("basket-place"));

        var first = await executor.ExecuteAsync(envelope, CancellationToken.None);
        var duplicate = await executor.ExecuteAsync(envelope, CancellationToken.None);

        Assert.Equal(first, duplicate);
        Assert.Equal(BasketballBetError.None, first.Error);
        Assert.Equal(50, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(1, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
        Assert.Equal(50, await Scalar<int>("SELECT amount FROM basketball_bets"));
        Assert.Equal("basketball", await Scalar<string>("SELECT game_id FROM mini_game_sessions"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
    }

    [Fact]
    public async Task Throw_CommitsPayoutStateAndSessionDeletionInboxAndEventsTogether()
    {
        await CreatePlaceExecutor().ExecuteAsync(
            new GameExecutionEnvelope<BasketballPlaceBetCommand>(Place("basket-place-throw")),
            CancellationToken.None);

        var result = await CreateThrowExecutor().ExecuteAsync(
            new GameExecutionEnvelope<BasketballThrowCommand>(new(42, "basket", 84, 5, "basket-throw", 0)),
            CancellationToken.None);

        Assert.Equal(BasketballThrowOutcome.Thrown, result.Outcome);
        Assert.Equal(100, result.Payout);
        Assert.Equal(150, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM basketball_bets"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM mini_game_sessions"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
    }

    [Fact]
    public async Task Abort_RefundsStakeRestoresQuotaAndDeletesStateAtomically()
    {
        await CreatePlaceExecutor().ExecuteAsync(
            new GameExecutionEnvelope<BasketballPlaceBetCommand>(Place("basket-place-abort")),
            CancellationToken.None);

        var result = await CreateAbortExecutor().ExecuteAsync(
            new GameExecutionEnvelope<BasketballAbortCommand>(new(42, "basket", 84, "basket-abort")),
            CancellationToken.None);

        Assert.True(result.Aborted);
        Assert.Equal(100, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(0, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM basketball_bets"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM mini_game_sessions"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
    }

    [Fact]
    public async Task EventFailure_RollsBackEveryPlaceBetMutation()
    {
        var executor = CreatePlaceExecutor(new FailingEventCollector());

        await Assert.ThrowsAsync<InjectedFailureException>(() => executor.ExecuteAsync(
            new GameExecutionEnvelope<BasketballPlaceBetCommand>(Place("basket-fail")),
            CancellationToken.None));

        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM users"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM telegram_dice_daily_rolls"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM basketball_bets"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM mini_game_sessions"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
    }

    private IAtomicGameExecutor<BasketballPlaceBetCommand, BasketballBetState, BasketballBetResult> CreatePlaceExecutor(
        ITransactionalEventCollector? events = null) =>
        CreateExecutor(
            events,
            new PlaceDescriptor(),
            new BasketballPlaceBetAction(),
            new BasketballBetStateStore());

    private IAtomicGameExecutor<BasketballThrowCommand, BasketballBetState, BasketballThrowResult> CreateThrowExecutor() =>
        CreateExecutor(
            null,
            new ThrowDescriptor(),
            new BasketballThrowAction(),
            new BasketballBetStateStore());

    private IAtomicGameExecutor<BasketballAbortCommand, BasketballBetState, BasketballAbortResult> CreateAbortExecutor() =>
        CreateExecutor(
            null,
            new AbortDescriptor(),
            new BasketballAbortAction(),
            new BasketballBetStateStore());

    private IAtomicGameExecutor<TCommand, BasketballBetState, TResult> CreateExecutor<TCommand, TResult>(
        ITransactionalEventCollector? events,
        GameExecutionDescriptor<TCommand, BasketballBetState, TResult> descriptor,
        IGameAction<TCommand, BasketballBetState, TResult> action,
        IGameStateStore<TCommand, BasketballBetState> stateStore)
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        var time = new FixedTimeProvider(Now);
        return new AtomicGameExecutor<TCommand, BasketballBetState, TResult>(
            new PostgresGameExecutionSessionFactory(connections),
            new PostgresCommandInbox(),
            new PostgresAtomicGameAvailability(new ConfigurationBuilder().Build()),
            new PostgresAtomicEconomics(Options.Create(new BotFrameworkOptions { StartingCoins = 100 })),
            new PostgresAtomicQuotaStore(),
            new PostgresAtomicPlayerProtection(time),
            events ?? new TransactionalEventCollector(new TestEventSerializer()),
            descriptor,
            action,
            stateStore,
            [],
            time,
            new GameExecutionTelemetry(NullLogger<GameExecutionTelemetry>.Instance));
    }

    private static BasketballPlaceBetCommand Place(string commandId) =>
        new(42, "basket", 84, 50, commandId, 100, null);

    private Task<T> Scalar<T>(string sql) => database.ScalarAsync<T>(sql);

    private abstract class Descriptor<TCommand, TResult>
        : GameExecutionDescriptor<TCommand, BasketballBetState, TResult>
    {
        public override string GameId => "basketball";
        public override string AggregateId(TCommand command) => "84:42";
        public override long ChatId(TCommand command) => 84;
        public override string DisplayName(TCommand command) => "basket";
        public override WalletIdentity Wallet(TCommand command) => new(42, 84);
        public override IReadOnlyList<QuotaIdentity> Quotas(TCommand command, DateTimeOffset utcNow) =>
            [new(
                BasketballPlaceBetAction.DailyRollQuota,
                GameId,
                42,
                84,
                DateOnly.FromDateTime(utcNow.UtcDateTime),
                10)];
    }

    private sealed class PlaceDescriptor : Descriptor<BasketballPlaceBetCommand, BasketballBetResult>
    {
        public override string CommandId(BasketballPlaceBetCommand command) => command.CommandId;
    }

    private sealed class ThrowDescriptor : Descriptor<BasketballThrowCommand, BasketballThrowResult>
    {
        public override IReadOnlyList<string> EntropyNames => [BasketballThrowAction.RedeemDropEntropy];
        public override string CommandId(BasketballThrowCommand command) => command.CommandId;
    }

    private sealed class AbortDescriptor : Descriptor<BasketballAbortCommand, BasketballAbortResult>
    {
        public override string CommandId(BasketballAbortCommand command) => command.CommandId;
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

    private sealed class FailingEventCollector : ITransactionalEventCollector
    {
        public Task AppendAsync(
            string commandId,
            IReadOnlyList<IDomainEvent> events,
            IGameExecutionSession session,
            CancellationToken ct) => throw new InjectedFailureException();
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

    private sealed class InjectedFailureException : Exception;
}
