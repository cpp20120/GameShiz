using System.Text.Json;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Economics.Options;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Games.Challenges.Application.Execution;
using Games.Challenges.Infrastructure.Persistence;
using Games.Redeem.Application.Execution;
using Games.Redeem.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicChallengeRedeemPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<BotFrameworkOptions> FrameworkOptions =
        Options.Create(new BotFrameworkOptions { StartingCoins = 100 });

    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Challenge_CreateAcceptCompleteCommitsStateWalletsInboxAndOutboxExactlyOnce()
    {
        var id = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var create = Executor(new ChallengeCreateDescriptor(), new ChallengeCreateAction(),
            new ChallengeExecutionStateStore<ChallengeCreateCommand>(
                new EconomicsService(new TestConnectionFactory(database.ConnectionString), FrameworkOptions,
                    NullLogger<EconomicsService>.Instance)));
        var created = await create.ExecuteAsync(new(new ChallengeCreateCommand(
            id, 1, "alice", new(2, "bob"), 10, 25, ChallengeGame.Dice,
            1, 1000, TimeSpan.FromMinutes(10), "challenge-create", [new(1, 10)])),
            CancellationToken.None);
        Assert.Equal(ChallengeCreateError.None, created.Error);

        var wallets = new ChallengeWalletRef[] { new(1, 10), new(2, 10) };
        var accept = Executor(new ChallengeAcceptDescriptor(), new ChallengeAcceptAction(),
            new ChallengeExecutionStateStore<ChallengeAcceptCommand>(
                new EconomicsService(new TestConnectionFactory(database.ConnectionString), FrameworkOptions,
                    NullLogger<EconomicsService>.Instance)));
        var acceptCommand = new ChallengeAcceptCommand(id, 2, "bob", 10, "challenge-accept", wallets);
        var accepted = await accept.ExecuteAsync(new(acceptCommand), CancellationToken.None);
        var duplicate = await accept.ExecuteAsync(new(acceptCommand), CancellationToken.None);
        Assert.Equal(accepted, duplicate);
        Assert.Equal(75, await Balance(1, 10));
        Assert.Equal(75, await Balance(2, 10));

        var complete = Executor(new ChallengeCompleteDescriptor(), new ChallengeCompleteAction(),
            new ChallengeExecutionStateStore<ChallengeCompleteCommand>(
                new EconomicsService(new TestConnectionFactory(database.ConnectionString), FrameworkOptions,
                    NullLogger<EconomicsService>.Instance)));
        var result = await complete.ExecuteAsync(new(new ChallengeCompleteCommand(
            id, 1, "alice", 10, 6, 3, 250, "challenge-complete", wallets)), CancellationToken.None);

        Assert.Equal(49, result.Payout);
        Assert.Equal(124, await Balance(1, 10));
        Assert.Equal(75, await Balance(2, 10));
        Assert.Equal("Completed", await Scalar<string>("SELECT status FROM challenge_duels"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
    }

    [Fact]
    public async Task Redeem_IssueAndClaimAtomicallyGrantsNegativeQuotaOnlyOnce()
    {
        var code = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var issue = Executor(new RedeemIssueDescriptor(), new RedeemIssueAction(), new RedeemIssueStateStore());
        Assert.Equal(code, await issue.ExecuteAsync(new(new RedeemIssueCommand(
            code, 1, MiniGameIds.Dice, "redeem-issue")), CancellationToken.None));

        var tuning = new FakeRuntimeTuning
        {
            TelegramDiceDailyLimit = new TelegramDiceDailyLimitOptions
            {
                MaxRollsPerUserPerDayByGame = new(StringComparer.Ordinal) { [MiniGameIds.Dice] = 10 },
            },
        };
        var complete = Executor(new RedeemCompleteDescriptor(tuning, FrameworkOptions),
            new RedeemCompleteAction(), new RedeemCompleteStateStore());
        var command = new RedeemCompleteCommand(code, 2, 20, MiniGameIds.Dice, "redeem-complete");
        var first = await complete.ExecuteAsync(new(command), CancellationToken.None);
        var duplicate = await complete.ExecuteAsync(new(command), CancellationToken.None);

        Assert.Equal(first, duplicate);
        Assert.Equal(RedeemError.None, first.Error);
        Assert.False(await Scalar<bool>("SELECT active FROM redeem_codes"));
        Assert.Equal(-1, await Scalar<int>("SELECT roll_count FROM telegram_dice_daily_rolls"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
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
