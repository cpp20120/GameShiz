using System.Text.Json;

namespace Games.Meta.Domain.Seasons;

public sealed class SeasonQuestRotationConfig
{
    public static SeasonQuestRotationConfig Default { get; } = new();

    public string Focus { get; init; } = "all-round";
    public string RarityBias { get; init; } = "normal";

    public static SeasonQuestRotationConfig FromSeason(MetaSeason season) =>
        FromJson(season.ConfigJson);

    public static SeasonQuestRotationConfig FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Default;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("quests", out var quests) ||
                quests.ValueKind != JsonValueKind.Object)
            {
                return Default;
            }

            return new SeasonQuestRotationConfig
            {
                Focus = Normalize(ReadString(quests, "focus", Default.Focus)),
                RarityBias = Normalize(ReadString(quests, "rarityBias", Default.RarityBias)),
            };
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    private static string ReadString(JsonElement parent, string propertyName, string fallback)
    {
        return parent.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Replace('_', '-');
        return string.IsNullOrWhiteSpace(normalized) ? "normal" : normalized;
    }
}
