using System.Collections.Concurrent;
using BotFramework.Discord;
using BotFramework.Discord.Commands;
using BotFramework.Discord.Interactions;
using BotFramework.Discord.Routing;
using Discord;
using Discord.WebSocket;
using Games.Redeem.Contracts;

namespace Games.Redeem.Discord;

public sealed class RedeemDiscordHandler(IRedeemClient client) : IDiscordMessageHandler
{
    private static readonly ConcurrentDictionary<long, Guid> Pending = new();
    public bool CanHandle(DiscordMessageContext context) => DiscordCommand.Is(context, "redeem");

    public async Task HandleAsync(DiscordMessageContext context)
    {
        var p = DiscordCommand.Parts(context);
        var uid = DiscordCommand.UserId(context);
        var scope = DiscordCommand.ScopeId(context);
        if (p.Length >= 3 && p[1].Equals("captcha", StringComparison.OrdinalIgnoreCase) && int.TryParse(p[2], System.Globalization.CultureInfo.InvariantCulture, out var choice) && Pending.TryGetValue(uid, out var codeGuid))
        {
            if (!await client.VerifyCaptchaAsync(uid, codeGuid, choice, context.CancellationToken))
            {
                await DiscordCommand.ReplyAsync(context, "Неверная капча.");
                return;
            }
            var done = await client.CompleteAsync(uid, scope, codeGuid, context.CancellationToken);
            Pending.TryRemove(uid, out _);
            await DiscordCommand.ReplyResultAsync(context, done, "Redeem");
            return;
        }
        if (p.Length != 2)
        {
            await DiscordCommand.ReplyAsync(context, "`redeem <code>` затем `redeem captcha <id>`");
            return;
        }
        var begun = await client.BeginAsync(uid, scope, DiscordCommand.DisplayName(context), p[1], context.CancellationToken);
        if (begun.Error != RedeemClientError.None)
        {
            await DiscordCommand.ReplyResultAsync(context, begun, "Redeem");
            return;
        }
        if (begun.Captcha is null)
        {
            var done = await client.CompleteAsync(uid, scope, begun.CodeGuid, context.CancellationToken);
            await DiscordCommand.ReplyResultAsync(context, done, "Redeem");
            return;
        }
        Pending[uid] = begun.CodeGuid;
        var options = string.Join("\n", begun.Captcha.Items.Select(x => $"`{x.Data}` — {x.Text}"));
        await DiscordCommand.ReplyAsync(context, $"**{begun.Captcha.Pattern}**\n{options}\nОтвет: `redeem captcha <id>`");
    }
}
