using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Scheduling.Abstractions;
using BotFramework.Sdk.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicScheduleEffectPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset DueAt = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ScheduleThenCancel_AreDeliveredInOrderAndScopedToAggregate()
    {
        await AppendAsync(
            "command-1",
            ScheduleEffect.Schedule(
                "turn-timeout",
                "blackjack.turn-timeout",
                DueAt,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["revision"] = "4" }));
        await AppendAsync("command-2", ScheduleEffect.Cancel("turn-timeout"));

        var scheduler = new RecordingScheduler();
        var dispatcher = CreateDispatcher(scheduler);

        await dispatcher.DispatchBatchAsync(CancellationToken.None);
        Assert.Single(scheduler.Scheduled);
        Assert.Empty(scheduler.Cancelled);
        var scheduled = scheduler.Scheduled[0];
        Assert.Equal("blackjack", await database.ScalarAsync<string>(
            "SELECT game_id FROM game_schedule_outbox WHERE schedule_id = @scheduleId",
            new { scheduleId = scheduled.ScheduleId }));
        Assert.Equal("blackjack:chat-1:game-1:turn-timeout", scheduled.ScheduleId);
        Assert.Equal("blackjack.turn-timeout", scheduled.JobKey);
        Assert.Equal(DueAt, scheduled.Schedule.RunAt);
        Assert.Equal("4", scheduled.Data!["revision"]);

        await dispatcher.DispatchBatchAsync(CancellationToken.None);
        Assert.Equal(["blackjack:chat-1:game-1:turn-timeout"], scheduler.Cancelled);
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT count(*) FROM game_schedule_outbox WHERE status = 'sent'"));
    }

    [Fact]
    public async Task RolledBackTransaction_DoesNotPublishScheduleEffect()
    {
        var factory = new PostgresGameExecutionSessionFactory(new TestConnectionFactory(database.ConnectionString));
        await using var session = await factory.BeginAsync(CancellationToken.None);
        await new TransactionalScheduleCollector().AppendAsync(
            "command-rollback",
            "blackjack",
            "chat-1:game-1",
            [ScheduleEffect.Schedule("turn-timeout", "blackjack.turn-timeout", DueAt)],
            session,
            CancellationToken.None);
        await session.RollbackAsync(CancellationToken.None);

        Assert.Equal(0, await database.ScalarAsync<int>("SELECT count(*) FROM game_schedule_outbox"));
    }

    [Fact]
    public async Task MultipleEffects_AreInsertedAsOneOrderedBatch()
    {
        var factory = new PostgresGameExecutionSessionFactory(new TestConnectionFactory(database.ConnectionString));
        await using var session = await factory.BeginAsync(CancellationToken.None);
        await new TransactionalScheduleCollector().AppendAsync(
            "command-batch",
            "blackjack",
            "chat-1:game-1",
            [
                ScheduleEffect.Schedule("first", "blackjack.first", DueAt),
                ScheduleEffect.Cancel("second"),
            ],
            session,
            CancellationToken.None);
        await session.CommitAsync(CancellationToken.None);

        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT count(*) FROM game_schedule_outbox WHERE command_id = 'command-batch'"));
        Assert.Equal("blackjack:chat-1:game-1:first", await database.ScalarAsync<string>(
            "SELECT schedule_id FROM game_schedule_outbox WHERE command_id = 'command-batch' AND effect_index = 0"));
        Assert.Equal("blackjack:chat-1:game-1:second", await database.ScalarAsync<string>(
            "SELECT schedule_id FROM game_schedule_outbox WHERE command_id = 'command-batch' AND effect_index = 1"));
    }

    private async Task AppendAsync(string commandId, ScheduleEffect effect)
    {
        var factory = new PostgresGameExecutionSessionFactory(new TestConnectionFactory(database.ConnectionString));
        await using var session = await factory.BeginAsync(CancellationToken.None);
        await new TransactionalScheduleCollector().AppendAsync(
            commandId,
            "blackjack",
            "chat-1:game-1",
            [effect],
            session,
            CancellationToken.None);
        await session.CommitAsync(CancellationToken.None);
    }

    private GameScheduleOutboxDispatcher CreateDispatcher(IGameScheduler scheduler) =>
        new(
            new PostgresGameScheduleOutbox(new TestConnectionFactory(database.ConnectionString)),
            scheduler,
            NullLogger<GameScheduleOutboxDispatcher>.Instance);

    private sealed class RecordingScheduler : IGameScheduler
    {
        public List<GameScheduleCommand> Scheduled { get; } = [];
        public List<string> Cancelled { get; } = [];

        public Task ScheduleAsync(GameScheduleCommand command, CancellationToken ct)
        {
            Scheduled.Add(command);
            return Task.CompletedTask;
        }

        public Task TriggerNowAsync(
            string jobKey,
            IReadOnlyDictionary<string, string> data,
            CancellationToken ct) => Task.CompletedTask;

        public Task UnscheduleAsync(string scheduleId, CancellationToken ct)
        {
            Cancelled.Add(scheduleId);
            return Task.CompletedTask;
        }
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
}
