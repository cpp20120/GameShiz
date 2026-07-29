using BotFramework.Host.Contracts.ResponsibleGaming;
using BotFramework.Sdk.Economics;

namespace BotFramework.Host.Economics;

internal static class PlayerProtectionGuard
{
    public static void EnsureAllowed(PlayerProtectionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Allowed) return;

        throw decision.ReasonCode switch
        {
            "self_excluded" => new PlayerProtectionException("self_excluded", decision.BlockedUntil),
            "cooldown" => new PlayerProtectionException("cooldown", decision.BlockedUntil),
            "daily_limit" => new PlayerProtectionException(
                "daily_limit", decision.DailyStakeLimit, decision.UsedToday),
            _ => new PlayerProtectionException(decision.ReasonCode ?? "player_protection"),
        };
    }
}
