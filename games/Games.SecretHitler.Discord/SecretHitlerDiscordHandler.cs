using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.SecretHitler.Application.Services;
using Games.SecretHitler.Domain.Results;

namespace Games.SecretHitler.Discord;

public sealed class SecretHitlerDiscordHandler(ISecretHitlerService service) : IDiscordMessageHandler
{
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.Is(context, "sh", "secrethitler");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        var p = DiscordCommand.Parts(context);
        var uid = DiscordCommand.UserId(context);
        object? r;
        if (p.Length < 2)
        {
            var x = await service.FindMyGameAsync(uid, context.CancellationToken);
            r = x.Snapshot;
        }
        else switch (p[1].ToLowerInvariant())
        {
            case "create": r = await service.CreateGameAsync(uid, DiscordCommand.DisplayName(context), DiscordCommand.ScopeId(context), DiscordCommand.ScopeId(context), context.CancellationToken); break;
            case "join" when p.Length > 2: r = await service.JoinGameAsync(uid, DiscordCommand.DisplayName(context), DiscordCommand.ScopeId(context), p[2], context.CancellationToken); break;
            case "start": r = await service.StartGameAsync(uid, context.CancellationToken); break;
            case "nominate" when p.Length > 2 && int.TryParse(p[2], System.Globalization.CultureInfo.InvariantCulture, out var pos): r = await service.NominateAsync(uid, pos, context.CancellationToken); break;
            case "vote" when p.Length > 2: r = await service.VoteAsync(uid, p[2].Equals("ja", StringComparison.OrdinalIgnoreCase) ? ShVote.Ja : ShVote.Nein, context.CancellationToken); break;
            case "discard" when p.Length > 2 && int.TryParse(p[2], System.Globalization.CultureInfo.InvariantCulture, out var di): r = await service.PresidentDiscardAsync(uid, di, context.CancellationToken); break;
            case "enact" when p.Length > 2 && int.TryParse(p[2], System.Globalization.CultureInfo.InvariantCulture, out var ei): r = await service.ChancellorEnactAsync(uid, ei, context.CancellationToken); break;
            case "leave": r = await service.LeaveAsync(uid, context.CancellationToken); break;
            case "state": var x = await service.FindMyGameAsync(uid, context.CancellationToken); r = x.Snapshot; break;
            default:
                await DiscordCommand.ReplyAsync(context, "`sh create|join <code>|start|state|nominate <pos>|vote ja|nein|discard <idx>|enact <idx>|leave`");
                return;
        }
        await DiscordCommand.ReplyResultAsync(context, r, "Secret Hitler");
    }
}
