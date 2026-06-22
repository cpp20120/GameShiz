using System.Text.Json;

namespace Games.Meta;

public sealed class SeasonRewardsConfig
{
    public static SeasonRewardsConfig Default { get; } = new();

    public IReadOnlyList<int> PlayerTop { get; init; } = [5_000, 2_500, 1_000];
    public IReadOnlyList<int> ClanTop { get; init; } = [10_000, 5_000, 2_500];

    public static SeasonRewardsConfig FromSeason(MetaSeason season) =>
        FromJson(season.ConfigJson);

    public static SeasonRewardsConfig FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Default;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("rewards", out var rewards) ||
                rewards.ValueKind != JsonValueKind.Object)
            {
                return Default;
            }

            return new SeasonRewardsConfig
            {
                PlayerTop = ReadArray(rewards, "playerTop", Default.PlayerTop),
                ClanTop = ReadArray(rewards, "clanTop", Default.ClanTop),
            };
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public int PlayerRewardForPlace(int place) => RewardForPlace(PlayerTop, place);
    public int ClanRewardForPlace(int place) => RewardForPlace(ClanTop, place);

    private static int RewardForPlace(IReadOnlyList<int> rewards, int place)
    {
        var index = place - 1;
        return index >= 0 && index < rewards.Count ? Math.Max(0, rewards[index]) : 0;
    }

    private static IReadOnlyList<int> ReadArray(JsonElement parent, string propertyName, IReadOnlyList<int> fallback)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            return fallback;

        var result = value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var number)
                ? Math.Max(0, number)
                : 0)
            .Where(x => x > 0)
            .Take(10)
            .ToArray();

        return result.Length == 0 ? fallback : result;
    }
}
