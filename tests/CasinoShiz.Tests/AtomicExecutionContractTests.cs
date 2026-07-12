using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Dice.Application.Execution;
using Games.DiceCube.Application.Execution;
using Games.Blackjack.Application.Execution;
using Games.Basketball.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class AtomicExecutionContractTests
{
    [Fact]
    public void FrameworkMigrations_ContainAtomicCommandInboxUpgrade()
    {
        var migration = Assert.Single(
            new FrameworkMigrations().Migrations,
            item => string.Equals(item.Id, "022_atomic_game_command_inbox", StringComparison.Ordinal));

        Assert.Contains("game_command_idempotency", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("result_type", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("aggregate_id", migration.Sql, StringComparison.Ordinal);

        var outbox = Assert.Single(
            new FrameworkMigrations().Migrations,
            item => string.Equals(item.Id, "023_atomic_game_event_outbox", StringComparison.Ordinal));
        Assert.Contains("UNIQUE (command_id, event_index)", outbox.Sql, StringComparison.Ordinal);

        var aggregateStates = Assert.Single(
            new FrameworkMigrations().Migrations,
            item => string.Equals(item.Id, "024_versioned_game_aggregate_states", StringComparison.Ordinal));
        Assert.Contains("game_aggregate_states", aggregateStates.Sql, StringComparison.Ordinal);
        Assert.Contains("PRIMARY KEY (game_id, aggregate_id)", aggregateStates.Sql, StringComparison.Ordinal);

        var scheduleOutbox = Assert.Single(
            new FrameworkMigrations().Migrations,
            item => string.Equals(item.Id, "025_atomic_game_schedule_outbox", StringComparison.Ordinal));
        Assert.Contains("game_schedule_outbox", scheduleOutbox.Sql, StringComparison.Ordinal);
        Assert.Contains("UNIQUE (command_id, effect_index)", scheduleOutbox.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionCapabilities_AreTransactionAware()
    {
        AssertSessionParameter(typeof(ICommandInbox));
        AssertSessionParameter(typeof(IAtomicEconomics));
        AssertSessionParameter(typeof(IAtomicQuotaStore));
        AssertSessionParameter(typeof(ITransactionalScheduleCollector));
    }

    [Fact]
    public void LockKeys_AreStableAndScoped()
    {
        var wallet = new WalletIdentity(42, -100);
        var quota = new QuotaIdentity(
            "dice.daily-roll",
            "dice",
            42,
            -100,
            new DateOnly(2026, 7, 11),
            10);

        Assert.Equal("wallet:-100:42", wallet.LockKey);
        Assert.Equal("quota:dice:-100:42:2026-07-11", quota.LockKey);
    }

    [Fact]
    public void DiceService_OrchestratesOnlyThroughAtomicExecutor()
    {
        var dependencies = typeof(DiceService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(typeof(IAtomicGameExecutor<DiceCommand, NoGameState, DicePlayResult>), dependencies);
        Assert.DoesNotContain(typeof(IEconomicsService), dependencies);
        Assert.DoesNotContain(typeof(IDomainEventBus), dependencies);
    }

    [Fact]
    public void BasketballService_OrchestratesOnlyThroughAtomicExecutors()
    {
        var dependencies = typeof(BasketballService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(
            typeof(IAtomicGameExecutor<BasketballPlaceBetCommand, BasketballBetState, BasketballBetResult>),
            dependencies);
        Assert.Contains(
            typeof(IAtomicGameExecutor<BasketballThrowCommand, BasketballBetState, BasketballThrowResult>),
            dependencies);
        Assert.DoesNotContain(typeof(IEconomicsService), dependencies);
        Assert.DoesNotContain(typeof(IDomainEventBus), dependencies);
        Assert.DoesNotContain(typeof(IAnalyticsService), dependencies);
    }

    [Fact]
    public void DiceCubeService_UsesAtomicExecutorForPlaceBetPilot()
    {
        var dependencies = typeof(DiceCubeService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(
            typeof(IAtomicGameExecutor<DiceCubePlaceBetCommand, DiceCubePlaceBetState, CubeBetResult>),
            dependencies);
        Assert.Contains(
            typeof(IAtomicGameExecutor<DiceCubeRollCommand, DiceCubePlaceBetState, CubeRollResult>),
            dependencies);
        Assert.Contains(
            typeof(IAtomicGameExecutor<DiceCubeAbortCommand, DiceCubePlaceBetState, DiceCubeAbortResult>),
            dependencies);
        Assert.DoesNotContain(typeof(IEconomicsService), dependencies);
        Assert.DoesNotContain(typeof(IDomainEventBus), dependencies);
        Assert.DoesNotContain(typeof(IAnalyticsService), dependencies);
        Assert.DoesNotContain(typeof(ITelegramDiceDailyRollLimiter), dependencies);
    }

    [Fact]
    public void BlackjackService_OrchestratesOnlyThroughAtomicExecutors()
    {
        var dependencies = typeof(BlackjackService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(
            typeof(IAtomicGameExecutor<BlackjackStartCommand, BlackjackGameState, BlackjackResult>),
            dependencies);
        Assert.Contains(
            typeof(IAtomicGameExecutor<BlackjackTurnCommand, BlackjackGameState, BlackjackResult>),
            dependencies);
        Assert.DoesNotContain(typeof(IEconomicsService), dependencies);
        Assert.DoesNotContain(typeof(IDomainEventBus), dependencies);
        Assert.DoesNotContain(typeof(IAnalyticsService), dependencies);
    }

    private static void AssertSessionParameter(Type capability)
    {
        Assert.All(
            capability.GetMethods(),
            method => Assert.Contains(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(IGameExecutionSession)));
    }
}
