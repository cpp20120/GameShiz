using Discord;
using Discord.WebSocket;

namespace BotFramework.Discord.Interactions;

public sealed class DiscordCasinoMenuHandler : IDiscordInteractionHandler
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
        SocketSlashCommand command => command.Data.Name == "casino",
        SocketMessageComponent component => component.Data.CustomId == "casino:menu",
        _ => false,
    };

    public async Task HandleAsync(DiscordInteractionContext context)
    {
        if (context.Interaction is SocketMessageComponent component)
        {
            var section = component.Data.Values.FirstOrDefault() ?? "games";
            var text = section switch
            {
                "games" => "Игры: `/blackjack`, `/poker`, `/secret-hitler`, `/pick`, `/horse`, `/pixelbattle`.",
                "economy" => "Экономика: `/profile balance`, `/profile daily`, `/transfer`, `/redeem`.",
                "social" => "Социальное: `/challenge`, `/profile top`, `/profile global-top`.",
                "admin" => "Администрирование: `/casino-admin` (доступно только allowlisted пользователям и ролям).",
                _ => "Используй slash-команды — Discord покажет доступные параметры автоматически.",
            };
            await DiscordInteraction.ReplyAsync(context, text, ephemeral: true);
            return;
        }

        var menu = new SelectMenuBuilder()
            .WithCustomId("casino:menu")
            .WithPlaceholder("Выбери раздел")
            .WithMinValues(1)
            .WithMaxValues(1)
            .AddOption("Игры", "games", "Карточные, PvP и мини-игры", new Emoji("🎮"))
            .AddOption("Экономика", "economy", "Баланс, бонусы, переводы и промокоды", new Emoji("💰"))
            .AddOption("Социальное", "social", "Челленджи и рейтинги", new Emoji("🏆"))
            .AddOption("Администрирование", "admin", "Защищённые admin-команды", new Emoji("🛡️"));

        var components = new ComponentBuilder().WithSelectMenu(menu).Build();
        await DiscordInteraction.ReplyAsync(
            context,
            "**CasinoShiz**\nВыбери раздел. Prefix-команды продолжают работать, но slash-команды дают подсказки и компоненты.",
            components,
            ephemeral: true);
    }
}
