using System.Globalization;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Games.Pick.Telegram;

public sealed class TelegramPickAnnouncementPublisher(
    ITelegramBotClient bot,
    ILocalizer localizer) : IPickAnnouncementPublisher
{
    public async Task PublishLotteryAsync(LotterySettleResult result, CancellationToken ct)
    {
        var text = result.Kind == LotterySettleKind.Cancelled
            ? string.Format(CultureInfo.InvariantCulture, Loc("lottery.cancelled"),
                result.Entries.Count, result.Row.Stake)
            : string.Format(CultureInfo.InvariantCulture, Loc("lottery.settled"),
                Winner(result.WinnerId, result.WinnerName), result.Pot, result.Fee,
                result.Payout, result.Entries.Count);
        await bot.SendMessage(result.Row.ChatId, text, parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    public async Task PublishDailyAsync(DailySettleResult result, CancellationToken ct)
    {
        if ((!result.Drawn || result.WinnerId is null) && result.TicketsTotal == 0)
            return;
        var day = result.Row.DayLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var text = !result.Drawn || result.WinnerId is null
            ? string.Format(CultureInfo.InvariantCulture, Loc("daily.sweep.cancelled"),
                day, result.TicketsTotal)
            : string.Format(CultureInfo.InvariantCulture, Loc("daily.sweep.settled"),
                day, Winner(result.WinnerId, result.WinnerName),
                result.WinnerTicketCount ?? 0, result.TicketsTotal,
                result.DistinctUsers, result.PotTotal, result.Fee, result.Payout);
        await bot.SendMessage(result.Row.ChatId, text, parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    private static string Winner(long? id, string? name) => string.IsNullOrEmpty(name)
        ? string.Create(CultureInfo.InvariantCulture, $"User ID: {id}")
        : WebUtility.HtmlEncode(name);
    private string Loc(string key) => localizer.Get("pick", key);
}
