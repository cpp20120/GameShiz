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

    public static bool TryGetComponentToken(
        DiscordInteractionContext context,
        IDiscordComponentTokenStore tokenStore,
        out DiscordComponentToken token)
    {
        token = new DiscordComponentToken(string.Empty, string.Empty);
        return context.Interaction is SocketMessageComponent component
            && tokenStore.TryResolve(component.Data.CustomId, out token);
    }

    public static string ComponentAction(DiscordInteractionContext context, IDiscordComponentTokenStore tokenStore) =>
        TryGetComponentToken(context, tokenStore, out var token) ? token.Action : string.Empty;

    public static Modal TextModal(
        string customId,
        string title,
        string inputId,
        string label,
        string placeholder,
        int minLength = 1,
        int maxLength = 100)
    {
        return new ModalBuilder()
            .WithCustomId(customId)
            .WithTitle(title)
            .AddTextInput(label, inputId, TextInputStyle.Short, placeholder, minLength, maxLength, required: true)
            .Build();
    }

    public static string? ModalValue(SocketModal modal, string inputId)
    {
        return modal.Data.Components
            .FirstOrDefault(component => string.Equals(component.CustomId, inputId, StringComparison.Ordinal))
            ?.Value;
    }

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
        var embed = DiscordEmbeds.Text(text, null, context.CultureCode);
        if (context.Interaction.HasResponded)
            await context.Interaction.FollowupAsync(text: null, embed: embed, components: components, ephemeral: ephemeral);
        else
            await context.Interaction.RespondAsync(text: null, embed: embed, components: components, ephemeral: ephemeral);
    }

    public static Task ReplyResultAsync<T>(
        DiscordInteractionContext context,
        T result,
        string? title = null,
        MessageComponent? components = null,
        bool ephemeral = false)
    {
        var embed = DiscordEmbeds.Result(result, title, context.CultureCode);
        if (context.Interaction.HasResponded)
            return context.Interaction.FollowupAsync(text: null, embed: embed, components: components, ephemeral: ephemeral);

        return context.Interaction.RespondAsync(text: null, embed: embed, components: components, ephemeral: ephemeral);
    }

}
