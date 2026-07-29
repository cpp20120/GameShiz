using BotFramework.Host.Execution;
using BotFramework.Sdk.Events.Contracts;
using BotFramework.Sdk.Execution;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class StatefulEffectSystemPropertyTests
{
    [Property(MaxTest = 200)]
    public Property Materialization_PreservesPhaseOrderAndCount(NonEmptyArray<int> commands)
    {
        var economy = new List<EconomyEffect>();
        var quotas = new List<QuotaEffect>();
        var records = new List<IGameRecord>();
        var custom = new List<IGameEffect>();
        var events = new List<IDomainEvent>();
        var schedules = new List<ScheduleEffect>();

        foreach (var rawCommand in commands.Get)
        {
            var value = Math.Abs((long)rawCommand);
            switch (value % 6)
            {
                case 0:
                    economy.Add(EconomyEffect.Debit(1 + value % 100, $"economy:{value}"));
                    break;
                case 1:
                    quotas.Add(QuotaEffect.Consume(value % 2 == 0 ? "daily" : "weekly"));
                    break;
                case 2:
                    records.Add(new TestRecord((int)(value % int.MaxValue)));
                    break;
                case 3:
                    custom.Add(value % 2 == 0
                        ? new TestCustomA((int)(value % int.MaxValue))
                        : new TestCustomB((int)(value % int.MaxValue)));
                    break;
                case 4:
                    events.Add(new TestEvent((int)(value % int.MaxValue)));
                    break;
                default:
                    schedules.Add(ScheduleEffect.Cancel($"schedule:{value}"));
                    break;
            }
        }

        var set = new GameEffectSet(economy, quotas, records, custom, events, schedules);
        var expected = new List<IGameEffect>(commands.Get.Length);
        expected.AddRange(economy);
        expected.AddRange(quotas);
        expected.AddRange(records);
        expected.AddRange(custom);
        expected.AddRange(events);
        expected.AddRange(schedules);

        var materialized = set.Materialize();
        var valid = set.Count == commands.Get.Length
            && materialized.Count == set.Count
            && materialized.SequenceEqual(expected)
            && materialized.All(effect => effect is not null);

        return valid
            .ToProperty()
            .Label($"commands={commands.Get.Length}, materialized={materialized.Count}");
    }

    [Property(MaxTest = 200)]
    public Property Planning_PreservesQuotaAndCustomEffectOrder(NonEmptyArray<int> commands)
    {
        var quotaEffects = new List<QuotaEffect>();
        var customEffects = new List<IGameEffect>();
        foreach (var rawCommand in commands.Get)
        {
            var value = Math.Abs((long)rawCommand);
            if (value % 3 == 0)
                quotaEffects.Add(QuotaEffect.Consume(value % 2 == 0 ? "daily" : "weekly"));
            if (value % 4 == 0)
                customEffects.Add(new TestCustomA((int)(value % int.MaxValue)));
            if (value % 5 == 0)
                customEffects.Add(new TestCustomB((int)(value % int.MaxValue)));
        }

        var handlerA = new TestCustomHandlerA();
        var handlerB = new TestCustomHandlerB();
        var plan = GameEffectPlan.Create(
            new GameDecision<NoGameState, int>(
                DecisionStatus.Accepted,
                new NoGameState(),
                1,
                [],
                quotaEffects,
                [],
                [],
                [],
                CustomEffects: customEffects),
            DeclaredQuotas,
            EmptyWriters,
            new Dictionary<Type, IGameEffectHandler>
            {
                [typeof(TestCustomA)] = handlerA,
                [typeof(TestCustomB)] = handlerB,
            });

        var valid = true;
        foreach (var quotaId in QuotaIds)
        {
            var expected = quotaEffects
                .Where(effect => string.Equals(effect.QuotaId, quotaId, StringComparison.Ordinal))
                .ToArray();
            valid &= plan.QuotaEffects.TryGetValue(quotaId, out var actual)
                ? actual.SequenceEqual(expected)
                : expected.Length == 0;
        }

        var expectedHandlers = new List<IGameEffectHandler>();
        if (customEffects.Any(effect => effect is TestCustomB)) expectedHandlers.Add(handlerB);
        if (customEffects.Any(effect => effect is TestCustomA)) expectedHandlers.Add(handlerA);
        valid &= plan.Custom.Select(batch => batch.Handler).SequenceEqual(expectedHandlers);
        valid &= plan.Custom.All(batch =>
            batch.Effects.SequenceEqual(customEffects.Where(effect => effect.GetType() == batch.Handler.EffectType)));

        return valid
            .ToProperty()
            .Label($"commands={commands.Get.Length}, quotas={quotaEffects.Count}, custom={customEffects.Count}");
    }

    [Property(MaxTest = 120)]
    public Property RejectedDecisions_OnlyAllowEventEffects(NonNegativeInt rawCommand)
    {
        var category = rawCommand.Get % 6;
        var decision = category switch
        {
            0 => DecisionWith(economy: [EconomyEffect.Debit(1, "rejected")]),
            1 => DecisionWith(quotas: [QuotaEffect.Consume("daily")]),
            2 => DecisionWith(records: [new TestRecord(1)]),
            3 => DecisionWith(custom: [new TestCustomA(1)]),
            4 => DecisionWith(events: [new TestEvent(1)]),
            _ => DecisionWith(schedules: [ScheduleEffect.Cancel("timeout")]),
        };

        var rejected = false;
        try
        {
            GameEffectPlan.Create(
                decision,
                DeclaredQuotas,
                EmptyWriters,
                new Dictionary<Type, IGameEffectHandler>
                {
                    [typeof(TestCustomA)] = new TestCustomHandlerA(),
                });
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        var valid = category == 4 ? !rejected : rejected;
        return valid
            .ToProperty()
            .Label($"category={category}, rejected={rejected}");
    }

    [Property(MaxTest = 120)]
    public Property NullEffects_AreRejectedBeforePlanning(NonNegativeInt rawCategory)
    {
        var decision = (rawCategory.Get % 6) switch
        {
            0 => DecisionWith(economy: new[] { (EconomyEffect)null! }),
            1 => DecisionWith(quotas: new[] { (QuotaEffect)null! }),
            2 => DecisionWith(records: new[] { (IGameRecord)null! }),
            3 => DecisionWith(custom: new[] { (IGameEffect)null! }),
            4 => DecisionWith(events: new[] { (IDomainEvent)null! }),
            _ => DecisionWith(schedules: new[] { (ScheduleEffect)null! }),
        };

        var rejected = false;
        try
        {
            GameEffectPlan.Create(decision, DeclaredQuotas, EmptyWriters);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        return rejected
            .ToProperty()
            .Label($"category={rawCategory.Get % 6}");
    }

    [Property(MaxTest = 120)]
    public Property AtomicPlans_WithNullEffects_AreRejectedBeforeSessionAccess(NonNegativeInt rawCommand)
    {
        var executor = new AtomicEffectExecutor(null!, null!, [], null!, null!);
        var envelope = new AtomicEffectExecutionEnvelope("effect-test", $"command-{rawCommand.Get}", "aggregate", ["lock"]);
        var plan = new AtomicEffectPlan<int>(0, new IAtomicEffect[] { null! });
        var rejected = false;

        try
        {
            executor.ExecuteAsync(envelope, plan, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        return rejected
            .ToProperty()
            .Label($"command={rawCommand.Get}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> Pipeline_CommandSequence_PreservesTransactionalPhaseOrder(
        NonEmptyArray<int> commands)
    {
        var trace = new EffectTrace();
        var pipeline = CreatePipeline(trace);
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            var scenario = Scenario(Math.Abs((long)rawCommand));
            var decision = new GameDecision<NoGameState, int>(
                DecisionStatus.Accepted,
                new NoGameState(),
                1,
                scenario.Economy,
                scenario.Quotas,
                scenario.Records,
                scenario.Events,
                scenario.Schedules,
                CustomEffects: scenario.Custom);
            var plan = pipeline.Plan(decision, DeclaredQuotas);
            var start = trace.Calls.Count;

            await pipeline.ApplyAsync(
                $"command-{rawCommand}",
                "effect-test",
                "aggregate",
                new NoGameState(),
                new NoGameState(),
                new WalletIdentity(42, 100),
                DeclaredQuotas,
                decision,
                plan,
                new TestExecutionContext(),
                null!,
                CancellationToken.None);

            var actual = trace.Calls.Skip(start).ToArray();
            var expected = ExpectedPipelineCalls(scenario);
            if (!actual.SequenceEqual(expected))
            {
                failure = $"expected=[{string.Join(',', expected)}], actual=[{string.Join(',', actual)}]";
                break;
            }
        }

        return (failure is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, calls={trace.Calls.Count}");
    }

    [Property(MaxTest = 120)]
    public Property ScheduleCommand_RoundTripsThroughEffectPayload(NonNegativeInt rawCommand)
    {
        var command = new ScheduledCommand(rawCommand.Get, $"payload-{rawCommand.Get}");
        var effect = ScheduleEffect.ScheduleCommand(
            "timeout",
            DateTimeOffset.UnixEpoch.AddSeconds(rawCommand.Get % 100_000),
            command);
        var restored = AtomicGameSchedule.DeserializeCommand<ScheduledCommand>(effect.Data!);

        return (effect.Kind == ScheduleEffectKind.Schedule
                && effect.JobKey == AtomicGameSchedule.JobKey<ScheduledCommand>()
                && restored == command)
            .ToProperty()
            .Label($"command={rawCommand.Get}");
    }

    private static readonly IReadOnlyDictionary<Type, IGameRecordWriter> EmptyWriters =
        new Dictionary<Type, IGameRecordWriter>();

    private static readonly string[] QuotaIds = ["daily", "weekly"];

    private static readonly IReadOnlyList<QuotaIdentity> DeclaredQuotas =
    [
        new("daily", "effect-test", 42, 100, new DateOnly(2026, 7, 28), 100),
        new("weekly", "effect-test", 42, 100, new DateOnly(2026, 7, 28), 100),
    ];

    private static GameDecision<NoGameState, int> DecisionWith(
        IReadOnlyList<EconomyEffect>? economy = null,
        IReadOnlyList<QuotaEffect>? quotas = null,
        IReadOnlyList<IGameRecord>? records = null,
        IReadOnlyList<IDomainEvent>? events = null,
        IReadOnlyList<ScheduleEffect>? schedules = null,
        IReadOnlyList<IGameEffect>? custom = null) =>
        new(
            DecisionStatus.Rejected,
            new NoGameState(),
            0,
            economy ?? [],
            quotas ?? [],
            records ?? [],
            events ?? [],
            schedules ?? [],
            CustomEffects: custom);

    private static TransactionalGameEffectPipeline<NoGameState, NoGameState, int> CreatePipeline(
        EffectTrace trace)
    {
        return new TransactionalGameEffectPipeline<NoGameState, NoGameState, int>(
            new RecordingEconomics(trace),
            new RecordingQuotaStore(trace),
            new RecordingProtection(trace),
            new RecordingEvents(trace),
            new RecordingStateStore(trace),
            [new RecordingRecordWriter(trace)],
            new RecordingSchedules(trace),
            [new TestCustomHandlerA(trace), new TestCustomHandlerB(trace)]);
    }

    private static EffectScenario Scenario(long value)
    {
        var economy = value % 2 == 0
            ? (IReadOnlyList<EconomyEffect>)[EconomyEffect.Debit(1 + value % 10, "scenario.bet")]
            : [];
        var quotas = value % 3 == 0
            ? (IReadOnlyList<QuotaEffect>)[QuotaEffect.Consume(value % 2 == 0 ? "daily" : "weekly")]
            : [];
        var records = value % 4 == 0
            ? (IReadOnlyList<IGameRecord>)[new TestRecord((int)(value % int.MaxValue))]
            : [];
        var custom = new List<IGameEffect>();
        if (value % 5 == 0) custom.Add(new TestCustomA((int)(value % int.MaxValue)));
        if (value % 7 == 0) custom.Add(new TestCustomB((int)(value % int.MaxValue)));
        var events = value % 2 != 0
            ? (IReadOnlyList<IDomainEvent>)[new TestEvent((int)(value % int.MaxValue))]
            : [];
        var schedules = value % 11 == 0
            ? (IReadOnlyList<ScheduleEffect>)[ScheduleEffect.Cancel($"schedule:{value}")]
            : [];
        return new(economy, quotas, records, custom, events, schedules);
    }

    private static IReadOnlyList<string> ExpectedPipelineCalls(EffectScenario scenario)
    {
        var expected = new List<string>();
        if (scenario.Economy.Count != 0)
        {
            expected.Add("protection");
            expected.Add("economy");
        }

        expected.Add("quota:daily");
        expected.Add("quota:weekly");
        expected.Add("state");
        expected.AddRange(scenario.Records.Select(record => $"record:{((TestRecord)record).Id}"));
        if (scenario.Custom.Any(effect => effect is TestCustomB)) expected.Add("custom:B");
        if (scenario.Custom.Any(effect => effect is TestCustomA)) expected.Add("custom:A");
        expected.Add("events");
        if (scenario.Schedules.Count != 0) expected.Add("schedules");
        return expected;
    }

    private sealed record EffectScenario(
        IReadOnlyList<EconomyEffect> Economy,
        IReadOnlyList<QuotaEffect> Quotas,
        IReadOnlyList<IGameRecord> Records,
        IReadOnlyList<IGameEffect> Custom,
        IReadOnlyList<IDomainEvent> Events,
        IReadOnlyList<ScheduleEffect> Schedules);

    private sealed record TestRecord(int Id) : IGameRecord;

    private sealed record TestCustomA(int Id) : IGameEffect;

    private sealed record TestCustomB(int Id) : IGameEffect;

    private sealed record TestEvent(int Id) : IDomainEvent
    {
        public string EventType => "effect.test";

        public long OccurredAt => Id;
    }

    private sealed record ScheduledCommand(int Id, string Value);

    private sealed class EffectTrace
    {
        public List<string> Calls { get; } = [];
    }

    private sealed class RecordingProtection(EffectTrace trace) : IAtomicPlayerProtection
    {
        public Task EnforceAsync(
            long userId,
            IReadOnlyList<EconomyEffect> effects,
            IGameExecutionSession session,
            CancellationToken ct)
        {
            trace.Calls.Add("protection");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEconomics(EffectTrace trace) : IAtomicEconomics
    {
        public Task EnsureAsync(
            WalletIdentity wallet,
            string displayName,
            IGameExecutionSession session,
            CancellationToken ct) => Task.CompletedTask;

        public Task<WalletSnapshot> LoadAsync(
            WalletIdentity wallet,
            IGameExecutionSession session,
            CancellationToken ct) => Task.FromResult(new WalletSnapshot(100));

        public Task<WalletMutationResult> ApplyAsync(
            WalletIdentity wallet,
            IReadOnlyList<EconomyEffect> effects,
            IGameExecutionSession session,
            string operationId,
            CancellationToken ct)
        {
            trace.Calls.Add("economy");
            return Task.FromResult(new WalletMutationResult(true, false, new WalletSnapshot(100)));
        }
    }

    private sealed class RecordingQuotaStore(EffectTrace trace) : IAtomicQuotaStore
    {
        public Task<QuotaSnapshot> LoadAsync(
            QuotaIdentity quota,
            IGameExecutionSession session,
            CancellationToken ct) => Task.FromResult(new QuotaSnapshot(0, quota.Limit));

        public Task<QuotaSnapshot> ApplyAsync(
            QuotaIdentity quota,
            IReadOnlyList<QuotaEffect> effects,
            IGameExecutionSession session,
            CancellationToken ct)
        {
            trace.Calls.Add($"quota:{quota.QuotaId}");
            return Task.FromResult(new QuotaSnapshot(0, quota.Limit));
        }
    }

    private sealed class RecordingStateStore(EffectTrace trace) : IGameStateStore<NoGameState, NoGameState>
    {
        public Task<NoGameState> LoadAsync(
            NoGameState command,
            IGameExecutionContext context,
            CancellationToken ct) => Task.FromResult(new NoGameState());

        public Task SaveAsync(
            NoGameState command,
            NoGameState state,
            IGameExecutionContext context,
            CancellationToken ct)
        {
            trace.Calls.Add("state");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRecordWriter(EffectTrace trace) : IGameRecordWriter
    {
        public Type RecordType => typeof(TestRecord);

        public Task WriteAsync(IGameRecord record, IGameExecutionContext context, CancellationToken ct)
        {
            trace.Calls.Add($"record:{((TestRecord)record).Id}");
            return Task.CompletedTask;
        }
    }

    private sealed class TestCustomHandlerA(EffectTrace? trace = null) : GameEffectHandler<TestCustomA>
    {
        public override int Order => 20;

        protected override Task ApplyBatchAsync(
            IReadOnlyList<TestCustomA> effects,
            IGameExecutionContext context,
            CancellationToken ct)
        {
            trace?.Calls.Add("custom:A");
            return Task.CompletedTask;
        }
    }

    private sealed class TestCustomHandlerB(EffectTrace? trace = null) : GameEffectHandler<TestCustomB>
    {
        public override int Order => 10;

        protected override Task ApplyBatchAsync(
            IReadOnlyList<TestCustomB> effects,
            IGameExecutionContext context,
            CancellationToken ct)
        {
            trace?.Calls.Add("custom:B");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEvents(EffectTrace trace) : ITransactionalEventCollector
    {
        public Task AppendAsync(
            string commandId,
            IReadOnlyList<IDomainEvent> events,
            IGameExecutionSession session,
            CancellationToken ct)
        {
            trace.Calls.Add("events");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSchedules(EffectTrace trace) : ITransactionalScheduleCollector
    {
        public Task AppendAsync(
            string commandId,
            string gameId,
            string aggregateId,
            IReadOnlyList<ScheduleEffect> effects,
            IGameExecutionSession session,
            CancellationToken ct)
        {
            trace.Calls.Add("schedules");
            return Task.CompletedTask;
        }
    }

    private sealed class TestExecutionContext : IGameExecutionContext
    {
        public Task<bool> ApplyWalletAsync(
            long userId,
            long balanceScopeId,
            IReadOnlyList<EconomyEffect> effects,
            string operationId,
            CancellationToken ct) => Task.FromResult(false);

        public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct) =>
            Task.FromResult(0);

        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct) =>
            Task.FromResult<T?>(default);
    }
}
