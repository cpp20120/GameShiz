using BotFramework.Host;
using BotFramework.Host.Composition;
using BotFramework.Sdk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CasinoShiz.Host.Debug;

[Command("/__debug_replay")]
public sealed class DebugReplayHandler(
    IEventReplayService replayService,
    IConfiguration configuration,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    public async Task HandleAsync(UpdateContext ctx)
    {
        var msg = ctx.Update.Message;
        if (msg?.Text is null) return;
        if (!configuration.GetValue("Debug:Enabled", defaultValue: true)) return;
        if (configuration.GetValue("Debug:RequireAdmin", defaultValue: true) &&
            !IsAnyAdmin(botOptions.Value, msg.From?.Id ?? 0)) return;

        var result = await replayService.RebuildProjectionAsync(nameof(DebugEsSmokeProjection), ctx.Ct);
        await ctx.Bot.SendMessage(
            msg.Chat.Id,
            $"projection={result.ProjectionName} seen={result.EventsSeen} applied={result.EventsApplied}",
            replyParameters: new ReplyParameters { MessageId = msg.MessageId },
            cancellationToken: ctx.Ct);
    }

    private static bool IsAnyAdmin(BotFrameworkOptions o, long userId)
    {
        foreach (var id in o.Admins)
            if (id == userId) return true;
        foreach (var id in o.ReadOnlyAdmins)
            if (id == userId) return true;
        return false;
    }
}
