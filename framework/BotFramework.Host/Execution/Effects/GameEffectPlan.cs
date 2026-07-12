using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

internal sealed class GameEffectPlan
{
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

        var declaredQuotaIds = declaredQuotas
            .Select(quota => quota.QuotaId)
            .ToHashSet(StringComparer.Ordinal);
        var groupedQuotas = effects.Quotas
            .GroupBy(effect => effect.QuotaId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<QuotaEffect>)group.ToArray(),
                StringComparer.Ordinal);
        var unknownQuota = groupedQuotas.Keys.FirstOrDefault(id => !declaredQuotaIds.Contains(id));
        if (unknownQuota is not null)
            throw new InvalidOperationException($"Decision targets undeclared quota '{unknownQuota}'.");

        var plannedRecords = new List<(IGameRecord, IGameRecordWriter)>(effects.Records.Count);
        foreach (var record in effects.Records)
        {
            if (!writers.TryGetValue(record.GetType(), out var writer))
                throw new InvalidOperationException($"No game record writer is registered for '{record.GetType()}'.");
            plannedRecords.Add((record, writer));
        }

        handlers ??= new Dictionary<Type, IGameEffectHandler>();
        var plannedCustom = effects.Custom
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

        if (effects.Economy.Any(effect => effect is null)
            || effects.Quotas.Any(effect => effect is null)
            || effects.Records.Any(effect => effect is null)
            || effects.Custom.Any(effect => effect is null)
            || effects.Events.Any(effect => effect is null)
            || effects.Schedules.Any(effect => effect is null))
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
