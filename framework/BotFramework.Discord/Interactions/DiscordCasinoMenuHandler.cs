using Discord;
using Discord.WebSocket;

namespace BotFramework.Discord.Interactions;

public sealed class DiscordCasinoMenuHandler(IDiscordComponentTokenStore tokens) : IDiscordInteractionHandler
{
    public IEnumerable<ApplicationCommandProperties> BuildCommands()
    {
        yield return new SlashCommandBuilder()
            .WithName("casino")
            .WithDescription("Открыть меню CasinoShiz")
            .Build();
    }

    public bool CanHandle(SocketInteraction interaction) => interaction switch
    {
        SocketSlashCommand command => string.Equals(command.Data.Name, "casino", StringComparison.Ordinal),
        SocketMessageComponent component => tokens.TryResolve(component.Data.CustomId, out var token)
            && string.Equals(token.Action, "casino:menu", StringComparison.Ordinal),
        _ => false,
    };

    public async Task HandleAsync(DiscordInteractionContext context)
    {
        if (context.Interaction is SocketMessageComponent component)
        {
            var section = component.Data.Values.FirstOrDefault() ?? "games";
            var text = section switch
            {
                "games" => DiscordLocalization.Get("casino.games", context.CultureCode),
                "economy" => DiscordLocalization.Get("casino.economy", context.CultureCode),
                "social" => DiscordLocalization.Get("casino.social", context.CultureCode),
                "admin" => DiscordLocalization.Get("casino.admin", context.CultureCode),
                _ => DiscordLocalization.Get("casino.fallback", context.CultureCode),
            };
            await DiscordInteraction.ReplyAsync(context, text, ephemeral: true);
            return;
        }

        var menu = new SelectMenuBuilder()
            .WithCustomId(tokens.Issue("casino:menu"))
            .WithPlaceholder(DiscordLocalization.Get("casino.menu.placeholder", context.CultureCode))
            .WithMinValues(1)
            .WithMaxValues(1)
            .AddOption(DiscordLocalization.Get("casino.games.label", context.CultureCode), "games", DiscordLocalization.Get("casino.games.hint", context.CultureCode), new Emoji("🎮"))
            .AddOption(DiscordLocalization.Get("casino.economy.label", context.CultureCode), "economy", DiscordLocalization.Get("casino.economy.hint", context.CultureCode), new Emoji("💰"))
            .AddOption(DiscordLocalization.Get("casino.social.label", context.CultureCode), "social", DiscordLocalization.Get("casino.social.hint", context.CultureCode), new Emoji("🏆"))
            .AddOption(DiscordLocalization.Get("casino.admin.label", context.CultureCode), "admin", DiscordLocalization.Get("casino.admin.hint", context.CultureCode), new Emoji("🛡️"));

        var components = new ComponentBuilder()
            .WithSelectMenu(menu)
            .WithButton(DiscordLocalization.Get("button.code", context.CultureCode), tokens.Issue("redeem:code-modal"), ButtonStyle.Primary, new Emoji("🎟️"), row: 1)
            .Build();
        await DiscordInteraction.ReplyAsync(
            context,
            DiscordLocalization.Get("casino.menu.description", context.CultureCode),
            components,
            ephemeral: true);
    }
}
