using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BotFramework.Contracts.Messaging;
using BotFramework.Discord.Routing;
using Discord;

namespace BotFramework.Discord.Commands;

public static class DiscordCommand
{
    public static string[] Parts(DiscordMessageContext context) =>
        context.CommandText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool Is(DiscordMessageContext context, params string[] names)
    {
        var parts = Parts(context);
        return parts.Length > 0 && names.Any(x => string.Equals(parts[0], x, StringComparison.OrdinalIgnoreCase));
    }

    public static long UserId(DiscordMessageContext context) => checked((long)context.Message.Author.Id);
    public static long ScopeId(DiscordMessageContext context) => checked((long)context.Message.Channel.Id);
    public static int SourceId(DiscordMessageContext context) => unchecked((int)context.Message.Id);
    public static string DisplayName(DiscordMessageContext context) => context.Message.Author.GlobalName ?? context.Message.Author.Username;

    public static RequestMetadata Metadata(DiscordMessageContext context) => RequestMetadata.Create(
        clientId: "discord",
        userId: context.Message.Author.Id.ToString(CultureInfo.InvariantCulture),
        scopeId: context.Message.Channel.Id.ToString(CultureInfo.InvariantCulture),
        culture: context.CultureCode);

    public static Task ReplyAsync(DiscordMessageContext context, string text, string? title = null, bool isError = false) =>
        context.Message.Channel.SendMessageAsync(
            text: null,
            embed: DiscordEmbeds.Text(text, title, context.CultureCode, isError),
            messageReference: new MessageReference(context.Message.Id));

    public static Task ReplyResultAsync<T>(DiscordMessageContext context, T result, string? title = null)
    {
        return context.Message.Channel.SendMessageAsync(
            text: null,
            embed: DiscordEmbeds.Result(result, title, context.CultureCode),
            messageReference: new MessageReference(context.Message.Id));
    }

    public static string FormatResult<T>(T result, string? title = null)
    {
        if (result is null) return title is null ? "Нет данных." : $"**{title}**\nНет данных.";
        if (result is string s) return title is null ? s : $"**{title}**\n{s}";
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(title)) sb.Append("**").Append(title).AppendLine("**");
        foreach (var property in result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var value = property.GetValue(result);
            if (value is null) continue;
            var rendered = value switch
            {
                string x => x,
                System.Collections.IEnumerable x when value is not string => JsonSerializer.Serialize(x),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            };
            sb.Append("**").Append(property.Name).Append(":** ").AppendLine(rendered);
        }
        return sb.Length == 0 ? result.ToString() ?? "OK" : sb.ToString().TrimEnd();
    }

    public static int RandomFace(int inclusiveMin, int inclusiveMax) =>
        System.Security.Cryptography.RandomNumberGenerator.GetInt32(inclusiveMin, inclusiveMax + 1);
}
