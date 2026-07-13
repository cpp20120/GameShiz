using BotFramework.Sdk.Events.Contracts;

namespace BotFramework.Sdk.Execution;

public sealed record GameDecision<TState, TResult>(
    DecisionStatus Status,
    TState NewState,
    TResult Result,
    IReadOnlyList<EconomyEffect> Economy,
    IReadOnlyList<QuotaEffect> Quotas,
    IReadOnlyList<IGameRecord> Records,
    IReadOnlyList<IDomainEvent> Events,
    IReadOnlyList<ScheduleEffect> Schedules,
    string? RejectionReason = null,
    IReadOnlyList<IGameEffect>? CustomEffects = null)
{
    public GameEffectSet EffectSet => new(Economy, Quotas, Records, CustomEffects ?? [], Events, Schedules);
}
