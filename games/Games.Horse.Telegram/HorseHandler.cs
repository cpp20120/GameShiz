// ─────────────────────────────────────────────────────────────────────────────
// HorseHandler — dispatches /horse (bet/result/info/help; bare /horse = help) and /horserun.
// /horserun (in a group): this chat's pool only. /horserun global: all chats merged (same as web admin).
// /horserun from private chat: global. /horserun is silently rejected for non-admins.
//
// Winner announcement is delayed after the GIF (see HorseOptions.AnnounceDelayMs / AnnounceDelay1v1Ms). The update's ct is
// tied to the per-update scope (polling iteration or webhook request) — it
// can be cancelled before the delay elapses. We use the host lifetime
// ApplicationStopping token so the announce survives the scope but still
// cancels cleanly on shutdown.
// ─────────────────────────────────────────────────────────────────────────────

using System.Globalization;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Games.Horse.Application.Handlers;

[Command("/horse")]
[Command("/horserun")]
public sealed partial class HorseHandler(
    IHorseService service,
    ILocalizer localizer,
    IOptions<HorseOptions> options,
    IHorseRaceNotifier notifier) : IUpdateHandler
{
    private readonly HorseOptions _opts = options.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var msg = ctx.Update.Message;
        if (msg?.Text == null) return;

        var userId = msg.From?.Id ?? 0;
        if (userId == 0) return;

        if (msg.Text.StartsWith("/horserun", StringComparison.OrdinalIgnoreCase))
        {
            await HandleRunAsync(ctx, msg, userId);
            return;
        }

        var parts = StripFirst(msg.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var action = parts.Length > 0 ? parts[0] : "";

        var reply = new ReplyParameters { MessageId = msg.MessageId };
        switch (action)
        {
            case "bet": await HandleBetAsync(ctx, msg, userId, parts); break;
            case "result": await HandleResultAsync(ctx, msg); break;
            case "info": await HandleInfoAsync(ctx, msg); break;
            case "":
            case "help":
                await ctx.Bot.SendMessage(msg.Chat.Id,
                    string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("help"), _opts.HorseCount, _opts.MinBetsToRun),
                    parseMode: ParseMode.Html, replyParameters: reply, cancellationToken: ctx.Ct);
                break;
            default:
                await ctx.Bot.SendMessage(msg.Chat.Id, string.Format(Loc("unknown_action"), action),
                    replyParameters: reply, cancellationToken: ctx.Ct);
                break;
        }
    }

    private async Task HandleBetAsync(UpdateContext ctx, Message msg, long userId, string[] parts)
    {
        var chatId = msg.Chat.Id;
        var reply = new ReplyParameters { MessageId = msg.MessageId };

        if (parts.Length < 2 || !int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out int horseId))
        {
            await ctx.Bot.SendMessage(chatId, string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("bet.no_horse"), _opts.HorseCount),
                replyParameters: reply, cancellationToken: ctx.Ct);
            return;
        }
        if (parts.Length < 3 || !int.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out int amount))
        {
            await ctx.Bot.SendMessage(chatId, Loc("bet.no_amount"),
                replyParameters: reply, cancellationToken: ctx.Ct);
            return;
        }

        var displayName = msg.From?.Username ?? msg.From?.FirstName ?? string.Create(CultureInfo.InvariantCulture, $"User ID: {userId}");
        var r = await service.PlaceBetAsync(userId, displayName, chatId, horseId, amount, msg.MessageId, ctx.Ct);

        var text = r.Error switch
        {
            HorseError.None => string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("bet.accepted"), r.Amount, r.HorseId),
            HorseError.InvalidHorseId => string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("bet.invalid_horse"), _opts.HorseCount),
            HorseError.InvalidAmount => string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("bet.invalid_amount"), r.RemainingCoins),
            _ => Loc("bet.failed"),
        };
        await ctx.Bot.SendMessage(chatId, text, replyParameters: reply, cancellationToken: ctx.Ct);
    }

    private async Task HandleRunAsync(UpdateContext ctx, Message msg, long userId)
    {
        var chatId = msg.Chat.Id;
        var reply = new ReplyParameters { MessageId = msg.MessageId };

        var parts = msg.Text!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sub = parts.Length > 1 ? parts[1] : "";
        var global = sub.Equals("global", StringComparison.OrdinalIgnoreCase)
            || sub.Equals("all", StringComparison.OrdinalIgnoreCase);
        HorseRunKind kind;
        long scopeForRun;
        if (global)
        {
            kind = HorseRunKind.Global;
            scopeForRun = 0;
        }
        else if (msg.Chat.Type is ChatType.Group or ChatType.Supergroup)
        {
            kind = HorseRunKind.ThisChat;
            scopeForRun = chatId;
        }
        else
        {
            kind = HorseRunKind.Global;
            scopeForRun = 0;
        }

        var outcome = await service.RunRaceAsync(userId, kind, scopeForRun, ctx.Ct);
        if (outcome.Error == HorseError.NotAdmin) return;
        if (outcome.Error == HorseError.NotEnoughBets)
        {
            await ctx.Bot.SendMessage(chatId, string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("run.not_enough_bets"), _opts.MinBetsToRun),
                replyParameters: reply, cancellationToken: ctx.Ct);
            return;
        }

        await ctx.Bot.SendMessage(chatId, Loc("run.started"),
            replyParameters: reply, cancellationToken: ctx.Ct);

        await notifier.SendResultGifsAsync(outcome, outcome.RaceDate, ctx.Ct);
        notifier.ScheduleWinnerAnnouncements(outcome);
    }

    private async Task HandleResultAsync(UpdateContext ctx, Message msg)
    {
        var reply = new ReplyParameters { MessageId = msg.MessageId };
        var r = await service.GetTodayResultAsync(msg.Chat.Id, ctx.Ct);

        if (r.Winner.HasValue && r.FileId != null)
        {
            await ctx.Bot.SendAnimation(msg.Chat.Id, InputFile.FromFileId(r.FileId),
                caption: string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("result.winner"), r.Winner.Value + 1),
                replyParameters: reply, cancellationToken: ctx.Ct);
        }
        else
        {
            await ctx.Bot.SendMessage(msg.Chat.Id, Loc("result.none"),
                replyParameters: reply, cancellationToken: ctx.Ct);
        }
    }

    private async Task HandleInfoAsync(UpdateContext ctx, Message msg)
    {
        var info = await service.GetTodayInfoAsync(msg.Chat.Id, ctx.Ct);
        var parts = new List<string> { string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("info.stakes_count"), info.BetsCount) };
        if (info.BetsCount > 0)
        {
            var koefs = string.Join('\n',
                info.Koefs.OrderBy(kv => kv.Key).Select(kv =>
                    string.Format(System.Globalization.CultureInfo.InvariantCulture, Loc("info.koef_line"), kv.Key + 1, kv.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))));
            parts.Add(Loc("info.koefs_header") + "\n" + koefs);
        }

        await ctx.Bot.SendMessage(msg.Chat.Id, string.Join("\n\n", parts), parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = msg.MessageId }, cancellationToken: ctx.Ct);
    }

    private static string StripFirst(string str)
    {
        var parts = str.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1].Trim() : "";
    }

    private string Loc(string key) => localizer.Get("horse", key);
}
