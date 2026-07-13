using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class GameEffectSystemTests
{
    [Fact]
    public void EffectSet_MaterializesInStablePhaseOrder()
    {
        var economy = EconomyEffect.Debit(5, "stake");
        var quota = QuotaEffect.Consume("daily");
        var record = new TestRecord();
        var domainEvent = new TestEvent();
        var schedule = ScheduleEffect.Cancel("timeout");
        var decision = Decision(
            economy: [economy],
            quotas: [quota],
            records: [record],
            events: [domainEvent],
            schedules: [schedule]);

        var materialized = decision.EffectSet.Materialize();

        Assert.Equal(5, decision.EffectSet.Count);
        Assert.Equal([economy, quota, record, domainEvent, schedule], materialized);
        Assert.All(materialized, effect => Assert.IsAssignableFrom<IGameEffect>(effect));
    }

    [Fact]
    public void Plan_RejectsMutationEffectsOnRejectedDecision()
    {
        var decision = Decision(
            status: DecisionStatus.Rejected,
            economy: [EconomyEffect.Debit(1, "invalid")]);

        var error = Assert.Throws<InvalidOperationException>(() =>
            GameEffectPlan.Create(decision, [], EmptyWriters));

        Assert.Contains("rejected decision", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plan_RejectsUndeclaredQuotaBeforeExecution()
    {
        var decision = Decision(quotas: [QuotaEffect.Consume("undeclared")]);

        var error = Assert.Throws<InvalidOperationException>(() =>
            GameEffectPlan.Create(decision, [], EmptyWriters));

        Assert.Contains("undeclared quota", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectedDecision_CanStillEmitCommittedDomainEvents()
    {
        var decision = Decision(
            status: DecisionStatus.Rejected,
            events: [new TestEvent()]);

        var plan = GameEffectPlan.Create(decision, [], EmptyWriters);

        Assert.Single(plan.Effects.Events);
    }

    [Fact]
    public void CustomEffects_AreGroupedIntoTypedMaterializedBatches()
    {
        var handler = new TestEffectHandler();
        var decision = Decision(custom: [new TestEffect(1), new TestEffect(2)]);

        var plan = GameEffectPlan.Create(
            decision,
            [],
            EmptyWriters,
            new Dictionary<Type, IGameEffectHandler> { [typeof(TestEffect)] = handler });

        var batch = Assert.Single(plan.Custom);
        Assert.Same(handler, batch.Handler);
        Assert.Equal([new TestEffect(1), new TestEffect(2)], batch.Effects);
    }

    private static readonly IReadOnlyDictionary<Type, IGameRecordWriter> EmptyWriters =
        new Dictionary<Type, IGameRecordWriter>();

    private static GameDecision<NoGameState, string> Decision(
        DecisionStatus status = DecisionStatus.Accepted,
        IReadOnlyList<EconomyEffect>? economy = null,
        IReadOnlyList<QuotaEffect>? quotas = null,
        IReadOnlyList<IGameRecord>? records = null,
        IReadOnlyList<IDomainEvent>? events = null,
        IReadOnlyList<ScheduleEffect>? schedules = null,
        IReadOnlyList<IGameEffect>? custom = null) =>
        new(
            status,
            new NoGameState(),
            "result",
            economy ?? [],
            quotas ?? [],
            records ?? [],
            events ?? [],
            schedules ?? [],
            CustomEffects: custom);

    private sealed record TestRecord : IGameRecord;

    private sealed record TestEffect(int Value) : IGameEffect;

    private sealed class TestEffectHandler : GameEffectHandler<TestEffect>
    {
        protected override Task ApplyBatchAsync(
            IReadOnlyList<TestEffect> effects,
            IGameExecutionContext context,
            CancellationToken ct) => Task.CompletedTask;
    }

    private sealed record TestEvent : IDomainEvent
    {
        public string EventType => "test.effect";

        public long OccurredAt => 1;
    }
}
