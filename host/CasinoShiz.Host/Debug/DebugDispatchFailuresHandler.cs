using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CasinoShiz.Host.Debug;

[Command("/__debug_dispatch_failures")]
public sealed class DebugDispatchFailuresHandler(
    IEventDispatchFailureStore failures,
    IConfiguration configuration,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    public async Task HandleAsync(UpdateContext ctx)
    {
        var msg = ctx.Update.Message;
        if (msg?.Text is null) return;
        if (!DebugAccess.IsAllowed(msg, configuration, botOptions.Value)) return;

        var rows = await failures.ListUnresolvedAsync(10, ctx.Ct);
        var lines = new List<string> { $"unresolved dispatch failures: {rows.Count}" };
        lines.AddRange(rows.Select(row => string.Create(CultureInfo.InvariantCulture, $"#{row.Id} {row.EventType} {row.StreamId}@{row.StreamVersion} retries={row.RetryCount}")));

        await ctx.Bot.SendMessage(
            msg.Chat.Id,
            string.Join('\n', lines),
            replyParameters: new ReplyParameters { MessageId = msg.MessageId },
            cancellationToken: ctx.Ct);
    }
}

