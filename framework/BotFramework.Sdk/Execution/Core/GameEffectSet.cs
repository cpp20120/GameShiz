using BotFramework.Sdk.Events.Contracts;

namespace BotFramework.Sdk.Execution;

/// <summary>
/// A fully materialized, typed set of effects emitted by one decision.
/// Categories preserve deterministic execution order without runtime reflection.
/// </summary>
public sealed record GameEffectSet(
    IReadOnlyList<EconomyEffect> Economy,
    IReadOnlyList<QuotaEffect> Quotas,
    IReadOnlyList<IGameRecord> Records,
    IReadOnlyList<IGameEffect> Custom,
    IReadOnlyList<IDomainEvent> Events,
    IReadOnlyList<ScheduleEffect> Schedules)
{
    public int Count => Economy.Count + Quotas.Count + Records.Count + Custom.Count + Events.Count + Schedules.Count;

    public IReadOnlyList<IGameEffect> Materialize()
    {
        if (Count == 0) return [];

        var effects = new List<IGameEffect>(Count);
        effects.AddRange(Economy);
        effects.AddRange(Quotas);
        effects.AddRange(Records);
        effects.AddRange(Custom);
        effects.AddRange(Events);
        effects.AddRange(Schedules);
        return effects;
    }
}
