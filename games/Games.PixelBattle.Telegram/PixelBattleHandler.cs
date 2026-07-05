using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Games.PixelBattle.Application.Handlers;

[Command("/pixelbattle")]
public sealed class PixelBattleHandler(
    ILocalizer localizer,
    IAnalyticsService analytics,
    IOptions<PixelBattleOptions> options) : IUpdateHandler
{
    public async Task HandleAsync(UpdateContext ctx)
    {
        var msg = ctx.Update.Message;
        if (msg is null) return;

        var reply = new ReplyParameters { MessageId = msg.MessageId };
        var webAppUrl = options.Value.WebAppUrl;
        if (string.IsNullOrWhiteSpace(webAppUrl))
        {
            TrackOpen(msg, "not_configured");
            await ctx.Bot.SendMessage(
                msg.Chat.Id,
                Loc("open.not_configured"),
                replyParameters: reply,
                cancellationToken: ctx.Ct);
            return;
        }

        var markup = new InlineKeyboardMarkup([
            [InlineKeyboardButton.WithWebApp(Loc("open.button"), new WebAppInfo(webAppUrl))],
        ]);

        TrackOpen(msg, "success");
        await ctx.Bot.SendMessage(
            msg.Chat.Id,
            Loc("open.text"),
            replyParameters: reply,
            replyMarkup: markup,
            cancellationToken: ctx.Ct);
    }

    private void TrackOpen(Message msg, string outcome) =>
        analytics.Track("pixelbattle", "opened", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["user_id"] = msg.From?.Id ?? 0,
            ["chat_id"] = msg.Chat.Id,
            ["chat_type"] = msg.Chat.Type.ToString().ToLowerInvariant(),
            ["outcome"] = outcome,
        });

    private string Loc(string key) => localizer.Get("pixelbattle", key);
}
