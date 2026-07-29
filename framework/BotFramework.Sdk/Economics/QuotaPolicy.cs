using BotFramework.Sdk.Execution;

namespace BotFramework.Sdk.Economics;

public static class QuotaPolicy
{
    public static QuotaPolicyDecision Apply(
        QuotaSnapshot state,
        string quotaId,
        IReadOnlyList<QuotaEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(quotaId);
        ArgumentNullException.ThrowIfNull(effects);

        if (state.Limit <= 0)
        {
            if (effects.Count != 0)
                throw new InvalidOperationException($"Unlimited quota '{quotaId}' cannot be mutated.");
            return new(true, false, 0, 0);
        }

        var used = state.Used;
        foreach (var effect in effects)
        {
            if (!string.Equals(effect.QuotaId, quotaId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Quota effect '{effect.QuotaId}' does not target '{quotaId}'.");
            if (effect.Amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(effects), effect.Amount, "Quota effect amount must be positive.");

            used = effect.Kind switch
            {
                QuotaEffectKind.Consume => checked(used + effect.Amount),
                QuotaEffectKind.Restore => Math.Max(0, used - effect.Amount),
                QuotaEffectKind.Grant => checked(used - effect.Amount),
                _ => throw new ArgumentOutOfRangeException(nameof(effects), effect.Kind, "Unknown quota effect kind."),
            };
        }

        return used > state.Limit
            ? new(false, true, state.Used, state.Limit)
            : new(true, false, used, state.Limit);
    }
}
