using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Blackjack.Contracts;

namespace Games.Blackjack.Discord;

public sealed class BlackjackDiscordHandler(IBlackjackClient client) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.Is(context, "blackjack", "bj");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        var p = DiscordCommand.Parts(context);
        var uid = DiscordCommand.UserId(context);
        if (p.Length < 2)
        {
            await DiscordCommand.ReplyAsync(context, "`blackjack start <bet>` | `blackjack hit|stand|double|state`");
            return;
        }

        object r;
        switch (p[1].ToLowerInvariant())
        {
            case "start" when p.Length >= 3 && int.TryParse(p[2], System.Globalization.CultureInfo.InvariantCulture, out var bet) && bet > 0:
                r = await client.StartAsync(uid, DiscordCommand.DisplayName(context), DiscordCommand.ScopeId(context), bet,
                    context.Message.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), context.CancellationToken);
                break;
            case "hit": r = await client.HitAsync(uid, context.CancellationToken); break;
            case "stand": r = await client.StandAsync(uid, context.CancellationToken); break;
            case "double": r = await client.DoubleAsync(uid, context.CancellationToken); break;
            case "state": r = await client.GetStateAsync(uid, context.CancellationToken); break;
            default:
                await DiscordCommand.ReplyAsync(context, "`blackjack start <bet>` | `blackjack hit|stand|double|state`");
                return;
        }

        await DiscordCommand.ReplyResultAsync(context, r, "Blackjack");
    }
}
