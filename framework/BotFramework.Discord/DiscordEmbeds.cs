using Discord;

namespace BotFramework.Discord;

public static class DiscordEmbeds
{
    private static readonly Color Accent = new(0x8B5CF6);
    private static readonly Color Error = new(0xEF4444);

    public static Embed Text(string description, string? title = null, string culture = "ru", bool isError = false) =>
        Build(description, title, culture, isError);

    public static Embed Result<T>(T result, string? title, string culture = "ru") =>
        Build(
            result is null
                ? DiscordLocalization.Get("data.empty", culture)
                : Commands.DiscordCommand.FormatResult(result, title: null),
            title,
            culture,
            LooksLikeError(result));

    public static Embed Build(string? description, string? title, string culture = "ru", bool isError = false)
    {
        var builder = new EmbedBuilder()
            .WithColor(isError ? Error : Accent)
            .WithFooter($"CasinoShiz · {DiscordLocalization.Normalize(culture)}")
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (!string.IsNullOrWhiteSpace(title))
            builder.WithTitle(title.Length > 256 ? title[..256] : title);

        var safeDescription = string.IsNullOrWhiteSpace(description)
            ? DiscordLocalization.Get("data.empty", culture)
            : description;
        builder.WithDescription(safeDescription.Length > 4096 ? safeDescription[..4093] + "..." : safeDescription);
        return builder.Build();
    }

    public static bool LooksLikeError<T>(T result)
    {
        if (result is null) return false;
        var error = result.GetType().GetProperty("Error")?.GetValue(result);
        return error is not null && !string.Equals(error.ToString(), "None", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(error.ToString(), "Ok", StringComparison.OrdinalIgnoreCase);
    }
}
