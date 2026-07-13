using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Games.Blackjack.Application.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicBlackjackPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Start_CommitsWalletStateInboxEventsAndTimeoutTogether()
    {
        var (command, result) = await StartActiveHandAsync();

        Assert.Equal(BlackjackError.None, result.Error);
        Assert.Null(result.Snapshot!.Outcome);
        Assert.Equal(90, await Scalar<int>(
            "SELECT coins FROM users WHERE telegram_user_id = @userId", new { command.UserId }));
        Assert.Equal(1, await Scalar<int>(
            "SELECT count(*) FROM economics_ledger WHERE telegram_user_id = @userId", new { command.UserId }));
        Assert.Equal(1L, await Scalar<long>(
            "SELECT version FROM game_aggregate_states WHERE game_id = 'blackjack' AND aggregate_id = @id",
            new { id = command.UserId.ToString(CultureInfo.InvariantCulture) }));
        Assert.Equal(1, await Scalar<int>(
            "SELECT count(*) FROM game_schedule_outbox WHERE command_id = @commandId",
            new { commandId = command.CommandId }));
        Assert.Equal(1, await Scalar<int>(
            "SELECT count(*) FROM game_command_idempotency WHERE idempotency_key = @commandId AND status = 'completed'",
            new { commandId = command.CommandId }));
    }

    [Fact]
    public async Task DuplicateStart_ReturnsInboxResultWithoutSecondDebitOrSchedule()
    {
        var (command, first) = await StartActiveHandAsync();
        var duplicate = await CreateStartExecutor().ExecuteAsync(
            new GameExecutionEnvelope<BlackjackStartCommand>(command),
            CancellationToken.None);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(duplicate));
        Assert.Equal(1, await Scalar<int>(
            "SELECT count(*) FROM economics_ledger WHERE telegram_user_id = @userId", new { command.UserId }));
        Assert.Equal(1, await Scalar<int>(
            "SELECT count(*) FROM game_schedule_outbox WHERE command_id = @commandId",
            new { commandId = command.CommandId }));
    }

    [Fact]
    public async Task Stand_CommitsSettlementStatePayoutAndTimeoutCancelTogether()
    {
        var (start, _) = await StartActiveHandAsync();
        var turn = new BlackjackTurnCommand(
            start.UserId,
            start.DisplayName,
            start.ChatId,
            BlackjackTurnKind.Stand,
            1,
            $"blackjack:stand:{start.CommandId}");

        var result = await CreateTurnExecutor().ExecuteAsync(
            new GameExecutionEnvelope<BlackjackTurnCommand>(turn),
            CancellationToken.None);

        Assert.NotNull(result.Snapshot?.Outcome);
        Assert.Equal(2L, await Scalar<long>(
            "SELECT version FROM game_aggregate_states WHERE game_id = 'blackjack' AND aggregate_id = @id",
            new { id = start.UserId.ToString(CultureInfo.InvariantCulture) }));
        Assert.Equal("2", await Scalar<string>(
            "SELECT state ->> 'status' FROM game_aggregate_states WHERE game_id = 'blackjack' AND aggregate_id = @id",
            new { id = start.UserId.ToString(CultureInfo.InvariantCulture) }));
        Assert.Equal(1, await Scalar<int>(
            "SELECT count(*) FROM game_schedule_outbox WHERE command_id = @commandId AND effect_kind = 'cancel'",
            new { commandId = turn.CommandId }));
    }

    [Fact]
    public async Task FailureAfterStateMutation_RollsBackEveryBlackjackEffect()
    {
        var command = StartCommand(999, "blackjack-fail");
        var executor = CreateStartExecutor(new FailingEventCollector());

        await Assert.ThrowsAsync<InjectedBlackjackFailure>(() => executor.ExecuteAsync(
            new GameExecutionEnvelope<BlackjackStartCommand>(command),
            CancellationToken.None));

        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM users WHERE telegram_user_id = 999"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM economics_ledger WHERE telegram_user_id = 999"));
        Assert.Equal(0, await Scalar<int>(
            "SELECT count(*) FROM game_aggregate_states WHERE game_id = 'blackjack' AND aggregate_id = '999'"));
        Assert.Equal(0, await Scalar<int>(
            "SELECT count(*) FROM game_command_idempotency WHERE idempotency_key = 'blackjack-fail'"));
        Assert.Equal(0, await Scalar<int>(
            "SELECT count(*) FROM game_schedule_outbox WHERE command_id = 'blackjack-fail'"));
    }

    private async Task<(BlackjackStartCommand Command, BlackjackResult Result)> StartActiveHandAsync()
    {
        for (var index = 0; index < 30; index++)
        {
            var command = StartCommand(500 + index, $"blackjack-start-{index}");
            var result = await CreateStartExecutor().ExecuteAsync(
                new GameExecutionEnvelope<BlackjackStartCommand>(command),
                CancellationToken.None);
            if (result.Snapshot?.Outcome is null)
                return (command, result);
        }
        throw new InvalidOperationException("Could not create an active blackjack hand.");
    }

    private IAtomicGameExecutor<BlackjackStartCommand, BlackjackGameState, BlackjackResult> CreateStartExecutor(
        ITransactionalEventCollector? eventCollector = null)
    {
        var descriptor = new BlackjackStartDescriptor();
        return CreateExecutor(
            descriptor,
            new BlackjackStartAction(),
            new PostgresJsonGameStateStore<BlackjackStartCommand, BlackjackGameState, BlackjackResult>(descriptor),
            eventCollector);
    }

    private IAtomicGameExecutor<BlackjackTurnCommand, BlackjackGameState, BlackjackResult> CreateTurnExecutor()
    {
        var descriptor = new BlackjackTurnDescriptor();
        return CreateExecutor(
            descriptor,
            new BlackjackTurnAction(),
            new PostgresJsonGameStateStore<BlackjackTurnCommand, BlackjackGameState, BlackjackResult>(descriptor));
    }

    private IAtomicGameExecutor<TCommand, BlackjackGameState, BlackjackResult> CreateExecutor<TCommand>(
        GameExecutionDescriptor<TCommand, BlackjackGameState, BlackjackResult> descriptor,
        IGameAction<TCommand, BlackjackGameState, BlackjackResult> action,
        IGameStateStore<TCommand, BlackjackGameState> stateStore,
        ITransactionalEventCollector? eventCollector = null)
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        var time = new FixedTimeProvider(Now);
        return new AtomicGameExecutor<TCommand, BlackjackGameState, BlackjackResult>(
            new PostgresGameExecutionSessionFactory(connections),
            new PostgresCommandInbox(),
            new PostgresAtomicGameAvailability(new ConfigurationBuilder().Build()),
            new PostgresAtomicEconomics(Options.Create(new BotFrameworkOptions { StartingCoins = 100 })),
            new PostgresAtomicQuotaStore(),
            new PostgresAtomicPlayerProtection(time),
            eventCollector ?? new TransactionalEventCollector(new TestEventSerializer()),
            descriptor,
            action,
            stateStore,
            [],
            time,
            new GameExecutionTelemetry(NullLogger<GameExecutionTelemetry>.Instance),
            new TransactionalScheduleCollector());
    }

    private static BlackjackStartCommand StartCommand(long userId, string commandId) =>
        new(userId, $"player-{userId}", 100, 10, commandId, 1, 100, 60_000);

    private Task<T> Scalar<T>(string sql, object? parameters = null) => database.ScalarAsync<T>(sql, parameters);

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
        public Task AppendAsync(
            string commandId,
            IReadOnlyList<IDomainEvent> events,
            IGameExecutionSession session,
            CancellationToken ct) => throw new InjectedBlackjackFailure();
    }

    private sealed class InjectedBlackjackFailure : Exception;
}
