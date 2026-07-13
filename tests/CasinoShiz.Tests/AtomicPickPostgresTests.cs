using System.Text.Json;
using BotFramework.Host.Composition.Builder;
using BotFramework.Host.Events.Serialization;
using BotFramework.Host.Execution;
using BotFramework.Host.Persistence.Connections;
using BotFramework.Sdk.Execution;
using Games.Pick.Application.Execution;
using Games.Pick.Application.Results;
using Games.Pick.Infrastructure.Persistence;
using Games.Pick.Domain.Results;
using Games.Pick.Infrastructure.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CasinoShiz.Tests;

[Collection(AtomicPostgresCollection.Name)]
public sealed class AtomicPickPostgresTests(AtomicPostgresFixture database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task WinningPick_CommitsEconomyStreakChainInboxAndOutboxOnce()
    {
        var executor = CreateExecutor();
        var command = Command("pick-atomic");
        var envelope = new GameExecutionEnvelope<PickCommand>(command);

        var first = await executor.ExecuteAsync(envelope, CancellationToken.None);
        var duplicate = await executor.ExecuteAsync(envelope, CancellationToken.None);

        Assert.Equal(first with { Variants = [], BackedIndices = [] }, duplicate with { Variants = [], BackedIndices = [] });
        Assert.Equal(first.Variants.ToArray(), duplicate.Variants.ToArray());
        Assert.Equal(first.BackedIndices.ToArray(), duplicate.BackedIndices.ToArray());
        Assert.True(first.Won);
        Assert.Equal(109, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(1, await Scalar<int>("SELECT streak FROM pick_streaks"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM pick_chains"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(1, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));

        var store = new PickChainStore(new TestConnectionFactory(database.ConnectionString));
        var claimed = await store.ClaimAsync(first.ChainGuid!.Value, CancellationToken.None);
        Assert.NotNull(claimed);
        Assert.Null(await store.ClaimAsync(first.ChainGuid.Value, CancellationToken.None));
    }

    [Fact]
    public async Task QuickLottery_OpenJoinAndSettlementCommitThroughEffectPipelineOnce()
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        var store = new PickLotteryStore(connections);
        var open = CreateExecutor(new QuickOpenDescriptor(), new QuickLotteryOpenAction(), new QuickLotteryOpenStateStore());
        var join = CreateExecutor(new QuickJoinDescriptor(), new QuickLotteryJoinAction(), new QuickLotteryJoinStateStore());

        var openCommand = new QuickLotteryOpenCommand(42, "a", 84, 20, "quick-open", 1, 100, 300);
        var first = await open.ExecuteAsync(new(openCommand), CancellationToken.None);
        var duplicate = await open.ExecuteAsync(new(openCommand), CancellationToken.None);
        Assert.Equal(first, duplicate);

        var joined = await join.ExecuteAsync(
            new(new QuickLotteryJoinCommand(43, "b", 84, "quick-join")), CancellationToken.None);
        Assert.Equal(LotteryJoinStatus.Ok, joined.Status);

        var row = await store.FindOpenByChatAsync(84, CancellationToken.None) ?? throw new InvalidOperationException();
        var entries = await store.ListEntriesAsync(row.Id, CancellationToken.None);
        var settle = CreateExecutor(
            new QuickSettleDescriptor(), new QuickLotterySettleAction(), new QuickLotterySettleStateStore(),
            [new PickWalletCreditEffectHandler()]);
        var settleCommand = new QuickLotterySettleCommand(row, entries, false, "quick-settle", 2, 0.05);
        var result = await settle.ExecuteAsync(new(settleCommand), CancellationToken.None);
        var settleDuplicate = await settle.ExecuteAsync(new(settleCommand), CancellationToken.None);

        Assert.Equal(result with { Entries = [] }, settleDuplicate with { Entries = [] });
        Assert.Equal(result.Entries.ToArray(), settleDuplicate.Entries.ToArray());
        Assert.Equal(LotterySettleKind.Settled, result.Kind);
        Assert.Equal(198, await Scalar<int>("SELECT sum(coins) FROM users"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(3, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        Assert.Equal("settled", await Scalar<string>("SELECT status FROM pick_lottery"));
    }

    [Fact]
    public async Task DailyLottery_BuyAndQuartzSettlementEffectsCommitOnce()
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        var store = new PickDailyLotteryStore(connections);
        var buy = CreateExecutor(new DailyBuyDescriptor(), new DailyBuyAction(), new DailyBuyStateStore());
        var day = new DateOnly(2026, 7, 14);
        var buyCommand = new DailyBuyCommand(
            42, "a", 84, 2, "daily-buy", day, Now.AddHours(12).UtcDateTime, 50, 3, 10);

        var first = await buy.ExecuteAsync(new(buyCommand), CancellationToken.None);
        var duplicate = await buy.ExecuteAsync(new(buyCommand), CancellationToken.None);
        Assert.Equal(first, duplicate);

        var row = await store.FindOpenByChatAsync(84, day, CancellationToken.None) ?? throw new InvalidOperationException();
        var summaries = await store.ListUserTicketCountsAsync(row.Id, CancellationToken.None);
        var tickets = summaries.SelectMany(summary => Enumerable.Repeat(
            new DailyTicketOwner(summary.UserId, summary.DisplayName), summary.TicketCount)).ToArray();
        var settle = CreateExecutor(
            new DailySettleDescriptor(), new DailySettleAction(), new DailySettleStateStore(),
            [new PickWalletCreditEffectHandler()]);
        var settleCommand = new DailySettleCommand(row, tickets, "daily-settle", 0.10);

        var result = await settle.ExecuteAsync(new(settleCommand), CancellationToken.None);
        var settleDuplicate = await settle.ExecuteAsync(new(settleCommand), CancellationToken.None);

        Assert.Equal(result, settleDuplicate);
        Assert.True(result.Drawn);
        Assert.Equal(90, await Scalar<int>("SELECT coins FROM users"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM economics_ledger"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_command_idempotency"));
        Assert.Equal(2, await Scalar<int>("SELECT count(*) FROM game_event_outbox"));
        Assert.Equal("settled", await Scalar<string>("SELECT status FROM pick_daily_lottery"));
    }

    private IAtomicGameExecutor<PickCommand, PickGameState, PickResult> CreateExecutor()
        => CreateExecutor(new PickExecutionDescriptor(), new WinningPickAction(), new PickGameStateStore(),
            [new PickChainOfferEffectHandler()]);

    private IAtomicGameExecutor<TCommand, TState, TResult> CreateExecutor<TCommand, TState, TResult>(
        GameExecutionDescriptor<TCommand, TState, TResult> descriptor,
        IGameAction<TCommand, TState, TResult> action,
        IGameStateStore<TCommand, TState> stateStore,
        IEnumerable<IGameEffectHandler>? handlers = null)
    {
        var connections = new TestConnectionFactory(database.ConnectionString);
        var time = new FixedTimeProvider(Now);
        return new AtomicGameExecutor<TCommand, TState, TResult>(
            new PostgresGameExecutionSessionFactory(connections),
            new PostgresCommandInbox(),
            new PostgresAtomicGameAvailability(new ConfigurationBuilder().Build()),
            new PostgresAtomicEconomics(Options.Create(new BotFrameworkOptions { StartingCoins = 100 })),
            new PostgresAtomicQuotaStore(),
            new PostgresAtomicPlayerProtection(time),
            new TransactionalEventCollector(new TestEventSerializer()),
            descriptor,
            action,
            stateStore,
            [],
            time,
            new GameExecutionTelemetry(NullLogger<GameExecutionTelemetry>.Instance),
            effectHandlers: handlers);
    }

    private static PickCommand Command(string commandId) =>
        new(42, "picker", 84, 10, ["a", "b"], [0], 0, true, commandId,
            2, 4, 100, 0.05, 0.5, 4, 5, 60);

    private Task<T> Scalar<T>(string sql) => database.ScalarAsync<T>(sql);

    private sealed class WinningPickAction : IGameAction<PickCommand, PickGameState, PickResult>
    {
        public GameDecision<PickGameState, PickResult> Decide(GameActionInput<PickGameState, PickCommand> input) =>
            new PickAction().Decide(input with
            {
                Entropy = new EntropyValue(new Dictionary<string, double>
                {
                    [PickAction.OutcomeEntropy] = 0,
                }),
            });
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
