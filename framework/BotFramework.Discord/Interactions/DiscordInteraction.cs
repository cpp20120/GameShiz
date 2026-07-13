using System.Globalization;
using BotFramework.Discord.Commands;
using Discord;
using Discord.WebSocket;

namespace BotFramework.Discord.Interactions;

public static class DiscordInteraction
{
    public static long UserId(DiscordInteractionContext context) => checked((long)context.Interaction.User.Id);
    public static long ScopeId(DiscordInteractionContext context) => checked((long)(context.Interaction.Channel?.Id ?? 0));
    public static int SourceId(DiscordInteractionContext context) => unchecked((int)context.Interaction.Id);
    public static string DisplayName(DiscordInteractionContext context) =>
        context.Interaction.User.GlobalName ?? context.Interaction.User.Username;

    public static SocketSlashCommandDataOption? Option(SocketSlashCommand command, string name) =>
        command.Data.Options.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    public static SocketSlashCommandDataOption? Subcommand(SocketSlashCommand command) =>
        command.Data.Options.FirstOrDefault(x => x.Type is ApplicationCommandOptionType.SubCommand or ApplicationCommandOptionType.SubCommandGroup);

    public static T? Value<T>(IEnumerable<SocketSlashCommandDataOption> options, string name)
    {
        var value = options.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        if (value is null) return default;
        if (value is T typed) return typed;
        return (T?)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    public static async Task ReplyAsync(
        DiscordInteractionContext context,
        string text,
        MessageComponent? components = null,
        bool ephemeral = false)
    {
        var safe = text.Length <= 1900 ? text : text[..1900];
        if (context.Interaction.HasResponded)
            await context.Interaction.FollowupAsync(safe, components: components, ephemeral: ephemeral);
        else
            await context.Interaction.RespondAsync(safe, components: components, ephemeral: ephemeral);
    }

    public static Task ReplyResultAsync<T>(
        DiscordInteractionContext context,
        T result,
        string? title = null,
        MessageComponent? components = null,
        bool ephemeral = false) =>
        ReplyAsync(context, DiscordCommand.FormatResult(result, title), components, ephemeral);
}
