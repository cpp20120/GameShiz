using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Blackjack.Contracts;

namespace Games.Blackjack.Discord;

public sealed class BlackjackDiscordHandler(IBlackjackClient client) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext c) => DiscordCommand.Is(c, "blackjack", "bj");

    public async Task HandleAsync(DiscordMessageContext c)
    {
        var p = DiscordCommand.Parts(c);
        var uid = DiscordCommand.UserId(c);
        if (p.Length < 2)
        {
            await DiscordCommand.ReplyAsync(c, "`blackjack start <bet>` | `blackjack hit|stand|double|state`");
            return;
        }

        object r;
        switch (p[1].ToLowerInvariant())
        {
            case "start" when p.Length >= 3 && int.TryParse(p[2], out var bet) && bet > 0:
                r = await client.StartAsync(uid, DiscordCommand.DisplayName(c), DiscordCommand.ScopeId(c), bet, c.Message.Id.ToString(), c.CancellationToken);
                break;
            case "hit": r = await client.HitAsync(uid, c.CancellationToken); break;
            case "stand": r = await client.StandAsync(uid, c.CancellationToken); break;
            case "double": r = await client.DoubleAsync(uid, c.CancellationToken); break;
            case "state": r = await client.GetStateAsync(uid, c.CancellationToken); break;
            default:
                await DiscordCommand.ReplyAsync(c, "`blackjack start <bet>` | `blackjack hit|stand|double|state`");
                return;
        }

        await DiscordCommand.ReplyResultAsync(c, r, "Blackjack");
    }
}

public sealed class BlackjackDiscordInteractionHandler(IBlackjackClient client) : IDiscordInteractionHandler
{
    public IEnumerable<ApplicationCommandProperties> BuildCommands()
    {
        yield return new SlashCommandBuilder()
            .WithName("blackjack")
            .WithDescription("Играть в Blackjack")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("start")
                .WithDescription("Начать новую раздачу")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("bet", ApplicationCommandOptionType.Integer, "Ставка", isRequired: true, minValue: 1))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("state")
                .WithDescription("Показать текущую раздачу")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .Build();
    }

    public bool CanHandle(SocketInteraction interaction) => interaction switch
    {
        SocketSlashCommand command => command.Data.Name == "blackjack",
        SocketMessageComponent component => component.Data.CustomId.StartsWith("blackjack:", StringComparison.Ordinal),
        _ => false,
    };

    public async Task HandleAsync(DiscordInteractionContext context)
    {
        var userId = DiscordInteraction.UserId(context);
        object result;

        if (context.Interaction is SocketSlashCommand command)
        {
            var subcommand = DiscordInteraction.Subcommand(command);
            if (subcommand?.Name == "start")
            {
                var bet = DiscordInteraction.Value<long>(subcommand.Options, "bet");
                result = await client.StartAsync(
                    userId,
                    DiscordInteraction.DisplayName(context),
                    DiscordInteraction.ScopeId(context),
                    checked((int)bet),
                    context.Interaction.Id.ToString(),
                    context.CancellationToken);
            }
            else
            {
                result = await client.GetStateAsync(userId, context.CancellationToken);
            }
        }
        else
        {
            var component = (SocketMessageComponent)context.Interaction;
            result = component.Data.CustomId switch
            {
                "blackjack:hit" => await client.HitAsync(userId, context.CancellationToken),
                "blackjack:stand" => await client.StandAsync(userId, context.CancellationToken),
                "blackjack:double" => await client.DoubleAsync(userId, context.CancellationToken),
                _ => await client.GetStateAsync(userId, context.CancellationToken),
            };
        }

        var controls = new ComponentBuilder()
            .WithButton("Ещё карту", "blackjack:hit", ButtonStyle.Primary, new Emoji("🃏"))
            .WithButton("Хватит", "blackjack:stand", ButtonStyle.Success, new Emoji("✋"))
            .WithButton("Удвоить", "blackjack:double", ButtonStyle.Secondary, new Emoji("💸"))
            .WithButton("Обновить", "blackjack:state", ButtonStyle.Secondary, new Emoji("🔄"))
            .Build();
        await DiscordInteraction.ReplyResultAsync(context, result, "Blackjack", controls, ephemeral: true);
    }
}

public static class BlackjackDiscordModule
{
    public static IServiceCollection AddBlackjackDiscord(this IServiceCollection services) => services
        .AddScoped<IDiscordMessageHandler, BlackjackDiscordHandler>()
        .AddScoped<IDiscordInteractionHandler, BlackjackDiscordInteractionHandler>();
}
