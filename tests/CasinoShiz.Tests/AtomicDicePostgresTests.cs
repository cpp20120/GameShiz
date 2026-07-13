using System.Text.Json;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Dapper;
using Games.Dice.Application.Execution;
using Games.Dice.Domain.Results;
using Games.Dice.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicDicePostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 10, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Execute_CommitsWalletLedgerQuotaHistoryInboxAndOutboxTogether()
    {
        var executor = CreateExecutor(startingCoins: 100);
        var result = await executor.ExecuteAsync(Envelope(Command(messageId: 1)), CancellationToken.None);

        Assert.Equal(DiceOutcome.Played, result.Outcome);
        Assert.Equal(result.NewBalance, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM dice_rolls"));
        Assert.Equal(1, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_command_idempotency WHERE status = 'completed'"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        Assert.Equal(result.Prize > 0 ? 2 : 1, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
    }

    [Theory]
    [InlineData(FailurePoint.Record)]
    [InlineData(FailurePoint.Events)]
    [InlineData(FailurePoint.InboxComplete)]
    public async Task FailureAfterEffects_RollsBackEveryMutation(FailurePoint point)
    {
        var executor = CreateExecutor(
            startingCoins: 100,
            writer: point == FailurePoint.Record ? new FailingRecordWriter() : new DiceRollRecordWriter(),
            events: point == FailurePoint.Events ? new FailingEventCollector() : null,
            inbox: point == FailurePoint.InboxComplete ? new FailingCompleteInbox() : null);

        await Assert.ThrowsAsync<InjectedFailureException>(() =>
            executor.ExecuteAsync(Envelope(Command(messageId: 2)), CancellationToken.None));

        await AssertDatabaseIsEmpty();
    }

    [Fact]
    public async Task DuplicateCommand_ReturnsStoredResultWithoutRepeatingEffects()
    {
        var executor = CreateExecutor(startingCoins: 100);
        var envelope = Envelope(Command(messageId: 3));

        var first = await executor.ExecuteAsync(envelope, CancellationToken.None);
        var duplicate = await executor.ExecuteAsync(envelope, CancellationToken.None);

        Assert.Equal(first, duplicate);
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM dice_rolls"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        Assert.Equal(first.Prize > 0 ? 2 : 1, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
    }

    [Fact]
    public async Task ConcurrentCommandsForSameAggregate_DoNotCreateNegativeBalance()
    {
        var executor = CreateExecutor(startingCoins: 10);
        var first = executor.ExecuteAsync(Envelope(Command(messageId: 4)), CancellationToken.None);
        var second = executor.ExecuteAsync(Envelope(Command(messageId: 5)), CancellationToken.None);

        var results = await Task.WhenAll(first, second);

        Assert.Contains(results, result => result.Outcome == DiceOutcome.Played);
        Assert.Contains(results, result => result.Outcome == DiceOutcome.NotEnoughCoins);
        Assert.Equal(5, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM dice_rolls"));
        Assert.Equal(1, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_command_idempotency WHERE status = 'completed'"));
    }

    [Fact]
    public async Task DifferentAggregateKeys_ReachMutationStageInParallel()
    {
        var probe = new ParallelRecordWriter();
        var executor = CreateExecutor(startingCoins: 100, writer: probe);

        var executions = Task.WhenAll(
            executor.ExecuteAsync(Envelope(Command(userId: 101, chatId: 1001, messageId: 6)), CancellationToken.None),
            executor.ExecuteAsync(Envelope(Command(userId: 202, chatId: 2002, messageId: 7)), CancellationToken.None));

        await executions.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(2, probe.MaximumConcurrentWriters);
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM dice_rolls"));
    }

    [Fact]
    public async Task CancellationBeforeCommit_RollsBackEverything()
    {
        var writer = new CancellationRecordWriter();
        var executor = CreateExecutor(startingCoins: 100, writer: writer);
        using var cancellation = new CancellationTokenSource();
        var execution = executor.ExecuteAsync(Envelope(Command(messageId: 8)), cancellation.Token);

        await writer.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        await AssertDatabaseIsEmpty();
    }

    [Fact]
    public async Task RetryAfterCommittedResponseLoss_IsRecoveredFromInbox()
    {
        var command = Command(messageId: 9);
        var committed = await CreateExecutor(startingCoins: 100)
            .ExecuteAsync(Envelope(command), CancellationToken.None);
        var retryExecutor = CreateExecutor(startingCoins: 100, action: new ThrowingAction());

        var recovered = await retryExecutor.ExecuteAsync(Envelope(command), CancellationToken.None);

        Assert.Equal(committed, recovered);
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM dice_rolls"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
    }

    private IAtomicGameExecutor<DiceCommand, NoGameState, DicePlayResult> CreateExecutor(
        int startingCoins,
        IGameRecordWriter? writer = null,
        ITransactionalEventCollector? events = null,
        ICommandInbox? inbox = null,
        IGameAction<DiceCommand, NoGameState, DicePlayResult>? action = null)
    {
        var connectionFactory = new TestConnectionFactory(database.ConnectionString);
        var time = new FixedTimeProvider(Now);
        return new AtomicGameExecutor<DiceCommand, NoGameState, DicePlayResult>(
            new PostgresGameExecutionSessionFactory(connectionFactory),
            inbox ?? new PostgresCommandInbox(),
            new PostgresAtomicGameAvailability(new ConfigurationBuilder().Build()),
            new PostgresAtomicEconomics(Options.Create(new BotFrameworkOptions { StartingCoins = startingCoins })),
            new PostgresAtomicQuotaStore(),
            new PostgresAtomicPlayerProtection(time),
            events ?? new TransactionalEventCollector(new TestEventSerializer()),
            new TestDiceDescriptor(),
            action ?? new DiceAction(),
            new DiceStateStore(),
            [writer ?? new DiceRollRecordWriter()],
            time,
            new GameExecutionTelemetry(NullLogger<GameExecutionTelemetry>.Instance));
    }

    private static GameExecutionEnvelope<DiceCommand> Envelope(DiceCommand command) => new(command);

    private static DiceCommand Command(
        long userId = 42,
        long chatId = 84,
        int messageId = 1) =>
        new(userId, "atomic-test", 37, chatId, messageId, false, 5, 0);

    private Task<T> Scalar<T>(string sql, object? parameters = null) => database.ScalarAsync<T>(sql, parameters);

    private async Task AssertDatabaseIsEmpty()
    {
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM users"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM telegram_dice_daily_rolls"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM dice_rolls"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        Assert.Equal(0, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
    }

    public enum FailurePoint
    {
        Record,
        Events,
        InboxComplete,
    }

    private sealed class TestDiceDescriptor : GameExecutionDescriptor<DiceCommand, NoGameState, DicePlayResult>
    {
        public override string GameId => "dice";
        public override string CommandId(DiceCommand command) => $"dice:test:{command.ChatId}:{command.SourceMessageId}:{command.UserId}";
        public override string AggregateId(DiceCommand command) => $"{command.ChatId}:{command.UserId}";
        public override long ChatId(DiceCommand command) => command.ChatId;
        public override string DisplayName(DiceCommand command) => command.DisplayName;
        public override WalletIdentity Wallet(DiceCommand command) => new(command.UserId, command.ChatId);
        public override IReadOnlyList<QuotaIdentity> Quotas(DiceCommand command, DateTimeOffset utcNow) =>
            [new(DiceAction.DailyRollQuota, GameId, command.UserId, command.ChatId, DateOnly.FromDateTime(utcNow.UtcDateTime), 10)];
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestEventSerializer : IEventSerializer
    {
        public string Serialize(IDomainEvent ev) => JsonSerializer.Serialize(ev, ev.GetType());
        public IDomainEvent? Deserialize(string eventType, string payloadJson) => throw new NotSupportedException();
    }

    private sealed class FailingRecordWriter : GameRecordWriter<DiceRollRecord>
    {
        protected override Task WriteAsync(DiceRollRecord record, IGameExecutionContext context, CancellationToken ct) =>
            throw new InjectedFailureException();
    }

    private sealed class ParallelRecordWriter : GameRecordWriter<DiceRollRecord>
    {
        private readonly TaskCompletionSource bothEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int active;
        private int entered;
        private int maximum;

        public int MaximumConcurrentWriters => maximum;

        protected override async Task WriteAsync(DiceRollRecord record, IGameExecutionContext context, CancellationToken ct)
        {
            var current = Interlocked.Increment(ref active);
            InterlockedExtensions.Max(ref maximum, current);
            if (Interlocked.Increment(ref entered) == 2) bothEntered.TrySetResult();
            try
            {
                await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                await WriteRecordAsync(record, context, ct).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }
    }

    private sealed class CancellationRecordWriter : GameRecordWriter<DiceRollRecord>
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Entered => entered.Task;

        protected override async Task WriteAsync(DiceRollRecord record, IGameExecutionContext context, CancellationToken ct)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
        }
    }

    private sealed class FailingEventCollector : ITransactionalEventCollector
    {
        public Task AppendAsync(string commandId, IReadOnlyList<IDomainEvent> events, IGameExecutionSession session, CancellationToken ct) =>
            throw new InjectedFailureException();
    }

    private sealed class FailingCompleteInbox : ICommandInbox
    {
        private readonly PostgresCommandInbox inner = new();

        public Task<CommandInboxResult<TResult>> GetOrBeginAsync<TResult>(string commandId, string gameId, string aggregateId, IGameExecutionSession session, CancellationToken ct) =>
            inner.GetOrBeginAsync<TResult>(commandId, gameId, aggregateId, session, ct);

        public Task StoreEntropyAsync(string commandId, EntropyValue entropy, IGameExecutionSession session, CancellationToken ct) =>
            inner.StoreEntropyAsync(commandId, entropy, session, ct);

        public Task CompleteAsync<TResult>(string commandId, TResult result, IGameExecutionSession session, CancellationToken ct) =>
            throw new InjectedFailureException();
    }

    private sealed class ThrowingAction : IGameAction<DiceCommand, NoGameState, DicePlayResult>
    {
        public GameDecision<NoGameState, DicePlayResult> Decide(GameActionInput<NoGameState, DiceCommand> input) =>
            throw new InjectedFailureException();
    }

    private static Task WriteRecordAsync(DiceRollRecord record, IGameExecutionContext context, CancellationToken ct) =>
        context.ExecuteAsync(
            """
            INSERT INTO dice_rolls (id, user_id, dice_value, prize, loss, rolled_at)
            VALUES (gen_random_uuid(), @UserId, @DiceValue, @Prize, @Loss, @RolledAt)
            """,
            record,
            ct);

    private sealed class InjectedFailureException : Exception;

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            var current = Volatile.Read(ref target);
            while (current < value)
            {
                var observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current) return;
                current = observed;
            }
        }
    }
}
