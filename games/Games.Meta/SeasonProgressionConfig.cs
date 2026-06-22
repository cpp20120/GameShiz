using System.Globalization;
using System.Text.Json;

namespace Games.Meta;

public sealed class SeasonProgressionConfig
{
    public static SeasonProgressionConfig Default { get; } = new();

    public int PlayXp { get; init; } = 5;
    public int WinXp { get; init; } = 25;
    public int LossXp { get; init; } = 2;
    public decimal StakeMultiplier { get; init; } = 0.01m;
    public int MaxXpPerGame { get; init; } = 500;
    public int MinXpPerGame { get; init; } = 1;

    public bool RatingEnabled { get; init; } = true;
    public int RatingStart { get; init; } = 1_000;
    public int RatingWinDelta { get; init; } = 16;
    public int RatingLossDelta { get; init; } = -12;

    public int XpPerLevelSquaredBase { get; init; } = 100;

    public static SeasonProgressionConfig FromSeason(MetaSeason season) =>
        FromJson(season.ConfigJson);

    public static SeasonProgressionConfig FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Default;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return Default;

            var root = doc.RootElement;
            var xp = TryGetObject(root, "xp");
            var rating = TryGetObject(root, "rating");
            var levels = TryGetObject(root, "levels") ?? TryGetObject(root, "progression");

            return new SeasonProgressionConfig
            {
                PlayXp = ReadInt(xp, "play", Default.PlayXp, 0, 10_000),
                WinXp = ReadInt(xp, "win", Default.WinXp, 0, 10_000),
                LossXp = ReadInt(xp, "loss", Default.LossXp, 0, 10_000),
                StakeMultiplier = ReadDecimal(xp, "stakeMultiplier", Default.StakeMultiplier, 0m, 10m),
                MaxXpPerGame = ReadInt(xp, "maxXpPerGame", Default.MaxXpPerGame, 1, 1_000_000),
                MinXpPerGame = ReadInt(xp, "minXpPerGame", Default.MinXpPerGame, 0, 1_000_000),
                RatingEnabled = ReadBool(rating, "enabled", Default.RatingEnabled),
                RatingStart = ReadInt(rating, "start", Default.RatingStart, 0, 1_000_000),
                RatingWinDelta = ReadInt(rating, "winDelta", Default.RatingWinDelta, -1_000_000, 1_000_000),
                RatingLossDelta = ReadInt(rating, "lossDelta", Default.RatingLossDelta, -1_000_000, 1_000_000),
                XpPerLevelSquaredBase = ReadInt(
                    levels,
                    "xpPerLevelSquaredBase",
                    Default.XpPerLevelSquaredBase,
                    1,
                    1_000_000),
            };
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public long CalculateXpDelta(long stake, bool isWin)
    {
        var baseXp = isWin ? WinXp : LossXp;
        var stakeXp = (long)Math.Floor(Math.Max(0, stake) * StakeMultiplier);
        var raw = PlayXp + baseXp + stakeXp;
        var max = Math.Max(MinXpPerGame, MaxXpPerGame);
        return Math.Clamp(raw, MinXpPerGame, max);
    }

    public int CalculateRatingDelta(bool isWin)
    {
        if (!RatingEnabled)
            return 0;

        return isWin ? RatingWinDelta : RatingLossDelta;
    }

    public int LevelForXp(long xp)
    {
        if (xp <= 0)
            return 1;

        return Math.Max(1, (int)Math.Floor(Math.Sqrt(xp / (double)XpPerLevelSquaredBase)) + 1);
    }

    public long XpForLevel(int level)
    {
        var normalized = Math.Max(1, level) - 1;
        return (long)XpPerLevelSquaredBase * normalized * normalized;
    }

    private static JsonElement? TryGetObject(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : null;
    }

    private static int ReadInt(JsonElement? parent, string propertyName, int fallback, int min, int max)
    {
        if (parent is not { } element || !element.TryGetProperty(propertyName, out var value))
            return fallback;

        var result = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number) => number,
            _ => fallback,
        };

        return Math.Clamp(result, min, max);
    }

    private static decimal ReadDecimal(JsonElement? parent, string propertyName, decimal fallback, decimal min, decimal max)
    {
        if (parent is not { } element || !element.TryGetProperty(propertyName, out var value))
            return fallback;

        var result = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(
                value.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var number) => number,
            _ => fallback,
        };

        return Math.Clamp(result, min, max);
    }

    private static bool ReadBool(JsonElement? parent, string propertyName, bool fallback)
    {
        if (parent is not { } element || !element.TryGetProperty(propertyName, out var value))
            return fallback;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback,
        };
    }
}
