using System.Text.Json;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Games.DiceCube.Application.Execution;
using Games.DiceCube.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicDiceCubePostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 10, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PlaceBet_CommitsDebitQuotaPendingStateInboxAndOutboxTogether()
    {
        var result = await CreateExecutor().ExecuteAsync(Envelope(Command("place-1")), CancellationToken.None);

        Assert.Equal(CubeBetError.None, result.Error);
        Assert.Equal(50, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(-50, await Scalar<int>("SELECT delta FROM economics_ledger"));
        Assert.Equal(1, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
        Assert.Equal(50, await Scalar<int>("SELECT amount FROM dicecube_bets"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_command_idempotency WHERE status = 'completed'"));
    }

    [Fact]
    public async Task FailureAfterStateSave_RollsBackBetWalletQuotaInboxAndOutbox()
    {
        var executor = CreateExecutor(new FailingEventCollector());

        await Assert.ThrowsAsync<InjectedFailureException>(() =>
            executor.ExecuteAsync(Envelope(Command("place-fail")), CancellationToken.None));

        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM users"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM telegram_dice_daily_rolls"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM dicecube_bets"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
    }

    [Fact]
    public async Task DuplicatePlaceCommand_ReturnsInboxResultWithoutSecondDebit()
    {
        var executor = CreateExecutor();
        var envelope = Envelope(Command("place-duplicate"));

        var first = await executor.ExecuteAsync(envelope, CancellationToken.None);
        var duplicate = await executor.ExecuteAsync(envelope, CancellationToken.None);

        Assert.Equal(first, duplicate);
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM dicecube_bets"));
        Assert.Equal(1, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
    }

    [Fact]
    public async Task ConcurrentPlaceCommands_SerializeAndOnlyOneCreatesPendingBet()
    {
        var executor = CreateExecutor();
        var results = await Task.WhenAll(
            executor.ExecuteAsync(Envelope(Command("place-a")), CancellationToken.None),
            executor.ExecuteAsync(Envelope(Command("place-b")), CancellationToken.None));

        Assert.Contains(results, result => result.Error == CubeBetError.None);
        Assert.Contains(results, result => result.Error == CubeBetError.AlreadyPending);
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM dicecube_bets"));
        Assert.Equal(1, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
    }

    [Fact]
    public async Task Roll_CommitsPayoutStateDeletionSessionDeletionInboxAndEventsTogether()
    {
        await CreateExecutor().ExecuteAsync(Envelope(Command("place-roll")), CancellationToken.None);

        var result = await CreateRollExecutor().ExecuteAsync(
            new GameExecutionEnvelope<DiceCubeRollCommand>(RollCommand("roll-1", 6)),
            CancellationToken.None);

        Assert.Equal(CubeRollOutcome.Rolled, result.Outcome);
        Assert.Equal(100, result.Payout);
        Assert.Equal(150, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM dicecube_bets"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM mini_game_sessions"));
        Assert.Equal(1, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_command_idempotency WHERE status = 'completed'"));
    }

    [Fact]
    public async Task RollFailureAfterStateDeletion_RollsBackPayoutAndKeepsPendingBet()
    {
        await CreateExecutor().ExecuteAsync(Envelope(Command("place-roll-fail")), CancellationToken.None);
        var roll = CreateRollExecutor(new FailingEventCollector());

        await Assert.ThrowsAsync<InjectedFailureException>(() => roll.ExecuteAsync(
            new GameExecutionEnvelope<DiceCubeRollCommand>(RollCommand("roll-fail", 6)),
            CancellationToken.None));

        Assert.Equal(50, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM dicecube_bets"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM mini_game_sessions"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
    }

    [Fact]
    public async Task DuplicateRollAfterBetDeletion_ReturnsInboxResultWithoutSecondPayout()
    {
        await CreateExecutor().ExecuteAsync(Envelope(Command("place-roll-duplicate")), CancellationToken.None);
        var executor = CreateRollExecutor();
        var envelope = new GameExecutionEnvelope<DiceCubeRollCommand>(RollCommand("roll-duplicate", 6));

        var first = await executor.ExecuteAsync(envelope, CancellationToken.None);
        var duplicate = await executor.ExecuteAsync(envelope, CancellationToken.None);

        Assert.Equal(first, duplicate);
        Assert.Equal(150, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM dicecube_bets"));
    }

    [Fact]
    public async Task Abort_RefundsWalletRestoresQuotaAndDeletesPendingStateAtomically()
    {
        await CreateExecutor().ExecuteAsync(Envelope(Command("place-abort")), CancellationToken.None);

        var result = await CreateAbortExecutor().ExecuteAsync(
            new GameExecutionEnvelope<DiceCubeAbortCommand>(new(42, "cube", 84, "abort-1")),
            CancellationToken.None);

        Assert.True(result.Aborted);
        Assert.Equal(100, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(0, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM dicecube_bets"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM mini_game_sessions"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
    }

    private IAtomicGameExecutor<DiceCubePlaceBetCommand, DiceCubePlaceBetState, CubeBetResult> CreateExecutor(
        ITransactionalEventCollector? events = null)
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        var time = new FixedTimeProvider(Now);
        return new AtomicGameExecutor<DiceCubePlaceBetCommand, DiceCubePlaceBetState, CubeBetResult>(
            new PostgresGameExecutionSessionFactory(connections),
            new PostgresCommandInbox(),
            new PostgresAtomicGameAvailability(new ConfigurationBuilder().Build()),
            new PostgresAtomicEconomics(Options.Create(new BotFrameworkOptions { StartingCoins = 100 })),
            new PostgresAtomicQuotaStore(),
            new PostgresAtomicPlayerProtection(time),
            events ?? new TransactionalEventCollector(new TestEventSerializer()),
            new TestDescriptor(),
            new DiceCubePlaceBetAction(),
            new DiceCubeBetStateStore(),
            [],
            time,
            new GameExecutionTelemetry(NullLogger<GameExecutionTelemetry>.Instance));
    }

    private IAtomicGameExecutor<DiceCubeRollCommand, DiceCubePlaceBetState, CubeRollResult> CreateRollExecutor(
        ITransactionalEventCollector? events = null) =>
        CreateSettlementExecutor(
            events,
            new RollDescriptor(),
            new DiceCubeRollAction(),
            new DiceCubeBetStateStore());

    private IAtomicGameExecutor<DiceCubeAbortCommand, DiceCubePlaceBetState, DiceCubeAbortResult> CreateAbortExecutor(
        ITransactionalEventCollector? events = null) =>
        CreateSettlementExecutor(
            events,
            new AbortDescriptor(),
            new DiceCubeAbortAction(),
            new DiceCubeBetStateStore());

    private IAtomicGameExecutor<TCommand, DiceCubePlaceBetState, TResult> CreateSettlementExecutor<TCommand, TResult>(
        ITransactionalEventCollector? events,
        GameExecutionDescriptor<TCommand, DiceCubePlaceBetState, TResult> descriptor,
        IGameAction<TCommand, DiceCubePlaceBetState, TResult> action,
        IGameStateStore<TCommand, DiceCubePlaceBetState> stateStore)
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        var time = new FixedTimeProvider(Now);
        return new AtomicGameExecutor<TCommand, DiceCubePlaceBetState, TResult>(
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

    private static DiceCubePlaceBetCommand Command(string commandId) =>
        new(42, "cube", 84, 50, commandId, 100, 1, 2, 2, 0, null);

    private static DiceCubeRollCommand RollCommand(string commandId, int face) =>
        new(42, "cube", 84, face, commandId, 0);

    private static GameExecutionEnvelope<DiceCubePlaceBetCommand> Envelope(DiceCubePlaceBetCommand command) => new(command);
    private Task<T> Scalar<T>(string sql) => database.ScalarAsync<T>(sql);

    private sealed class TestDescriptor
        : GameExecutionDescriptor<DiceCubePlaceBetCommand, DiceCubePlaceBetState, CubeBetResult>
    {
        public override string GameId => "dicecube";
        public override string CommandId(DiceCubePlaceBetCommand command) => command.CommandId;
        public override string AggregateId(DiceCubePlaceBetCommand command) => $"{command.ChatId}:{command.UserId}";
        public override long ChatId(DiceCubePlaceBetCommand command) => command.ChatId;
        public override string DisplayName(DiceCubePlaceBetCommand command) => command.DisplayName;
        public override WalletIdentity Wallet(DiceCubePlaceBetCommand command) => new(command.UserId, command.ChatId);
        public override IReadOnlyList<QuotaIdentity> Quotas(DiceCubePlaceBetCommand command, DateTimeOffset utcNow) =>
            [new(DiceCubePlaceBetAction.DailyRollQuota, GameId, command.UserId, command.ChatId, DateOnly.FromDateTime(utcNow.UtcDateTime), 10)];
    }

    private abstract class SettlementDescriptor<TCommand, TResult>
        : GameExecutionDescriptor<TCommand, DiceCubePlaceBetState, TResult>
    {
        public override string GameId => "dicecube";
        public override string AggregateId(TCommand command) => "84:42";
        public override long ChatId(TCommand command) => 84;
        public override string DisplayName(TCommand command) => "cube";
        public override WalletIdentity Wallet(TCommand command) => new(42, 84);
        public override IReadOnlyList<QuotaIdentity> Quotas(TCommand command, DateTimeOffset utcNow) =>
            [new(DiceCubePlaceBetAction.DailyRollQuota, GameId, 42, 84, DateOnly.FromDateTime(utcNow.UtcDateTime), 10)];
    }

    private sealed class RollDescriptor : SettlementDescriptor<DiceCubeRollCommand, CubeRollResult>
    {
        public override IReadOnlyList<string> EntropyNames => [DiceCubeRollAction.RedeemDropEntropy];
        public override string CommandId(DiceCubeRollCommand command) => command.CommandId;
    }

    private sealed class AbortDescriptor : SettlementDescriptor<DiceCubeAbortCommand, DiceCubeAbortResult>
    {
        public override string CommandId(DiceCubeAbortCommand command) => command.CommandId;
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

    private sealed class FailingEventCollector : ITransactionalEventCollector
    {
        public Task AppendAsync(string commandId, IReadOnlyList<IDomainEvent> events, IGameExecutionSession session, CancellationToken ct) =>
            throw new InjectedFailureException();
    }

    private sealed class InjectedFailureException : Exception;
}
