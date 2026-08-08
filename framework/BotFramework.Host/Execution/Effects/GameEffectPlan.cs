using System.Collections.ObjectModel;
using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal sealed class GameEffectPlan
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<QuotaEffect>> EmptyQuotaEffects =
        new ReadOnlyDictionary<string, IReadOnlyList<QuotaEffect>>(
            new Dictionary<string, IReadOnlyList<QuotaEffect>>(StringComparer.Ordinal));
    private static readonly IReadOnlyList<(IGameRecord Record, IGameRecordWriter Writer)> EmptyRecords =
        Array.Empty<(IGameRecord Record, IGameRecordWriter Writer)>();
    private static readonly IReadOnlyList<(IGameEffectHandler Handler, IReadOnlyList<IGameEffect> Effects)> EmptyCustom =
        Array.Empty<(IGameEffectHandler Handler, IReadOnlyList<IGameEffect> Effects)>();

    private GameEffectPlan(
        GameEffectSet effects,
        IReadOnlyDictionary<string, IReadOnlyList<QuotaEffect>> quotaEffects,
        IReadOnlyList<(IGameRecord Record, IGameRecordWriter Writer)> records,
        IReadOnlyList<(IGameEffectHandler Handler, IReadOnlyList<IGameEffect> Effects)> custom)
    {
        Effects = effects;
        QuotaEffects = quotaEffects;
        Records = records;
        Custom = custom;
    }

    public GameEffectSet Effects { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<QuotaEffect>> QuotaEffects { get; }

    public IReadOnlyList<(IGameRecord Record, IGameRecordWriter Writer)> Records { get; }

    public IReadOnlyList<(IGameEffectHandler Handler, IReadOnlyList<IGameEffect> Effects)> Custom { get; }

    public static GameEffectPlan Create<TState, TResult>(
        GameDecision<TState, TResult> decision,
        IReadOnlyList<QuotaIdentity> declaredQuotas,
        IReadOnlyDictionary<Type, IGameRecordWriter> writers,
        IReadOnlyDictionary<Type, IGameEffectHandler>? handlers = null)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var effects = decision.EffectSet;
        ValidateMaterialized(effects);

        if (decision.Status != DecisionStatus.Accepted
            && (effects.Economy.Count != 0
                || effects.Quotas.Count != 0
                || effects.Records.Count != 0
                || effects.Custom.Count != 0
                || effects.Schedules.Count != 0))
        {
            throw new InvalidOperationException("A rejected decision cannot contain mutation effects.");
        }

        IReadOnlyDictionary<string, IReadOnlyList<QuotaEffect>> groupedQuotas = EmptyQuotaEffects;
        if (effects.Quotas.Count != 0)
        {
            var declaredQuotaIds = declaredQuotas
                .Select(quota => quota.QuotaId)
                .ToHashSet(StringComparer.Ordinal);
            groupedQuotas = effects.Quotas
                .GroupBy(effect => effect.QuotaId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<QuotaEffect>)group.ToArray(),
                    StringComparer.Ordinal);
            var unknownQuota = groupedQuotas.Keys.FirstOrDefault(id => !declaredQuotaIds.Contains(id));
            if (unknownQuota is not null)
                throw new InvalidOperationException($"Decision targets undeclared quota '{unknownQuota}'.");
        }

        IReadOnlyList<(IGameRecord Record, IGameRecordWriter Writer)> plannedRecords = EmptyRecords;
        if (effects.Records.Count != 0)
        {
            var records = new List<(IGameRecord, IGameRecordWriter)>(effects.Records.Count);
            foreach (var record in effects.Records)
            {
                if (!writers.TryGetValue(record.GetType(), out var writer))
                    throw new InvalidOperationException($"No game record writer is registered for '{record.GetType()}'.");
                records.Add((record, writer));
            }

            plannedRecords = records;
        }

        IReadOnlyList<(IGameEffectHandler Handler, IReadOnlyList<IGameEffect> Effects)> plannedCustom = EmptyCustom;
        if (effects.Custom.Count != 0)
        {
            handlers ??= new Dictionary<Type, IGameEffectHandler>();
            plannedCustom = effects.Custom
                .GroupBy(effect => effect.GetType())
                .Select(group =>
                {
                    if (IsBuiltInEffect(group.Key))
                        throw new InvalidOperationException($"Built-in effect '{group.Key}' must use its typed decision category.");
                    if (!handlers.TryGetValue(group.Key, out var handler))
                        throw new InvalidOperationException($"No game effect handler is registered for '{group.Key}'.");
                    return (handler, (IReadOnlyList<IGameEffect>)group.ToArray());
                })
                .OrderBy(item => item.handler.Order)
                .ThenBy(item => item.handler.EffectType.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        return new GameEffectPlan(effects, groupedQuotas, plannedRecords, plannedCustom);
    }

    private static void ValidateMaterialized(GameEffectSet effects)
    {
        if (effects.Economy is null
            || effects.Quotas is null
            || effects.Records is null
            || effects.Custom is null
            || effects.Events is null
            || effects.Schedules is null)
        {
            throw new InvalidOperationException("A decision must materialize every effect category.");
        }

        if (effects.Economy.Any(static effect => effect is null)
            || effects.Quotas.Any(static effect => effect is null)
            || effects.Records.Any(static effect => effect is null)
            || effects.Custom.Any(static effect => effect is null)
            || effects.Events.Any(static effect => effect is null)
            || effects.Schedules.Any(static effect => effect is null))
        {
            throw new InvalidOperationException("A decision cannot contain null effects.");
        }
    }

    private static bool IsBuiltInEffect(Type type) =>
        typeof(EconomyEffect).IsAssignableFrom(type)
        || typeof(QuotaEffect).IsAssignableFrom(type)
        || typeof(IGameRecord).IsAssignableFrom(type)
        || typeof(IDomainEvent).IsAssignableFrom(type)
        || typeof(ScheduleEffect).IsAssignableFrom(type);
}
