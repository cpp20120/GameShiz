namespace BotFramework.Sdk.Economics;

public static class PlayerProtectionPolicy
{
    public static PlayerProtectionDecision Evaluate(
        PlayerProtectionState state,
        long stake,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stake);

        if (state.SelfExcludedUntil is { } excluded && excluded > utcNow)
            return new(false, "self_excluded", excluded, state.DailyStakeLimit, state.UsedToday);

        if (state.CooldownUntil is { } cooldown && cooldown > utcNow)
            return new(false, "cooldown", cooldown, state.DailyStakeLimit, state.UsedToday);

        if (state.DailyStakeLimit is { } limit && checked(state.UsedToday + stake) > limit)
            return new(false, "daily_limit", null, limit, state.UsedToday);

        return new(true, null, null, state.DailyStakeLimit, state.UsedToday);
    }
}
