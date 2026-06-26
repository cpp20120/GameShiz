using System.Text.Json.Nodes;

namespace BotFramework.Host.Configuration;

/// <summary>
/// Whitelists JSON keys allowed in
/// <c>runtime_tuning.payload</c> (admin-edited overlay).
/// </summary>
public static class RuntimeTuningPayloadSanitizer
{
    private static readonly HashSet<string> AllowedBotKeys =
        new(StringComparer.Ordinal)
        {
            "DailyBonus",
            "TelegramDiceDailyLimit",
        };

    private static readonly HashSet<string> AllowedGameKeys =
        new(StringComparer.Ordinal)
        {
            // Telegram-dice family
            "dice",
            "dicecube",
            "darts",
            "football",
            "basketball",
            "bowling",

            // Bigger games
            "horse",
            "poker",
            "blackjack",
            "sh",

            // Casino-style
            "pick",

            // PvP / utilities
            "challenges",
            "transfer",

            // Front-end / admin / cross-cutting
            "pixelbattle",
            "redeem",
            "leaderboard",
            "admin",
            "meta",
        };

    public static JsonObject Sanitize(JsonObject raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var result = new JsonObject();

        if (raw["Bot"] is JsonObject bot)
        {
            var sanitizedBot = SanitizeSection(bot, AllowedBotKeys);

            if (sanitizedBot.Count > 0)
                result["Bot"] = sanitizedBot;
        }

        if (raw["Games"] is not JsonObject games) return result;
        var sanitizedGames = SanitizeSection(games, AllowedGameKeys);

        if (sanitizedGames.Count > 0)
            result["Games"] = sanitizedGames;

        return result;
    }

    private static JsonObject SanitizeSection(
        JsonObject source,
        IReadOnlySet<string> allowedKeys)
    {
        ArgumentNullException.ThrowIfNull(allowedKeys);
        return new JsonObject(
            source
                .Where(property => allowedKeys.Contains(property.Key))
                .Select(property =>
                    new KeyValuePair<string, JsonNode?>(
                        property.Key,
                        property.Value?.DeepClone())));
    }
}