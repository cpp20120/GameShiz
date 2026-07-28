using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Poker.Application.Services;

namespace Games.Poker.Discord;

public sealed class PokerDiscordHandler(IPokerService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.Is(context, "poker");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        var p = DiscordCommand.Parts(context);
        var uid = DiscordCommand.UserId(context);
        var scope = DiscordCommand.ScopeId(context);
        object? r;
        if (p.Length < 2)
        {
            var x = await service.FindMyTableAsync(uid, scope, context.CancellationToken);
            r = x.Snapshot;
        }
        else switch (p[1].ToLowerInvariant())
        {
            case "create": r = await service.CreateTableAsync(uid, DiscordCommand.DisplayName(context), scope, DiscordCommand.SourceId(context), context.CancellationToken); break;
            case "join" when p.Length > 2: r = await service.JoinTableAsync(uid, DiscordCommand.DisplayName(context), scope, p[2], DiscordCommand.SourceId(context), context.CancellationToken); break;
            case "start": r = await service.StartHandAsync(uid, scope, context.CancellationToken); break;
            case "check": case "call": case "fold": r = await service.ApplyPlayerActionAsync(uid, scope, p[1], 0, context.CancellationToken); break;
            case "raise" when p.Length > 2 && int.TryParse(p[2], System.Globalization.CultureInfo.InvariantCulture, out var amount): r = await service.ApplyPlayerActionAsync(uid, scope, "raise", amount, context.CancellationToken); break;
            case "leave": r = await service.LeaveTableAsync(uid, scope, context.CancellationToken); break;
            case "state": var x = await service.FindMyTableAsync(uid, scope, context.CancellationToken); r = x.Snapshot; break;
            default:
                await DiscordCommand.ReplyAsync(context, "`poker create|join <code>|start|state|check|call|fold|raise <amount>|leave`");
                return;
        }
        await DiscordCommand.ReplyResultAsync(context, r, "Poker");
    }
}
