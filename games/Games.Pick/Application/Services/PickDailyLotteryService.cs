using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;
using Microsoft.Extensions.Options;

namespace Games.Pick.Application.Services;

public sealed class PickDailyLotteryService(
    IPickDailyLotteryStore store,
    IAtomicGameExecutor<DailyBuyCommand, DailyLotteryState, DailyBuyResult> buy,
    IAtomicGameExecutor<DailySettleCommand, DailyLotteryState, DailySettleResult> settle,
    IOptions<PickOptions> pickOptions,
    IOptions<TelegramDiceDailyLimitOptions> dice) : IPickDailyLotteryService
{
    private PickDailyLotteryOptions Options => pickOptions.Value.Daily;

    public int OffsetHours => Options.TimezoneOffsetHoursOverride != 0
        ? Options.TimezoneOffsetHoursOverride
        : dice.Value.TimezoneOffsetHours;

    public int DrawHourLocal => Math.Clamp(Options.DrawHourLocal, 0, 23);

    public DateOnly LocalToday()
    {
        var offset = TimeSpan.FromHours(OffsetHours);
        var now = DateTimeOffset.UtcNow.ToOffset(offset);
        var draw = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            DrawHourLocal,
            0,
            0,
            offset);
        return DateOnly.FromDateTime(now <= draw ? now.Date : now.Date.AddDays(1));
    }

    public DateTime LocalNextDrawUtc()
    {
        var day = LocalToday();
        return new DateTimeOffset(
            day.Year,
            day.Month,
            day.Day,
            DrawHourLocal,
            0,
            0,
            TimeSpan.FromHours(OffsetHours)).UtcDateTime;
    }

    public Task<DailyBuyResult> BuyAsync(
        long userId,
        string displayName,
        long chatId,
        int count,
        CancellationToken ct) =>
        BuyAsync(userId, displayName, chatId, count, 0, ct);

    public Task<DailyBuyResult> BuyAsync(
        long userId,
        string displayName,
        long chatId,
        int count,
        int sourceMessageId,
        CancellationToken ct)
    {
        var dailyOptions = Options;
        var day = LocalToday();
        var commandId = sourceMessageId != 0
            ? $"pick:daily:buy:{chatId}:{sourceMessageId}:{userId}"
            : $"pick:daily:buy:legacy:{Guid.NewGuid():N}";
        var command = new DailyBuyCommand(
            userId,
            displayName,
            chatId,
            count,
            commandId,
            day,
            LocalNextDrawUtc(),
            Math.Max(1, dailyOptions.TicketPrice),
            dailyOptions.MaxTicketsPerBuyCommand,
            dailyOptions.MaxTicketsPerUserPerDay);
        return buy.ExecuteAsync(new(command), ct);
    }

    public async Task<DailyInfoSnapshot?> InfoAsync(long chatId, long viewerId, CancellationToken ct)
    {
        var row = await store.FindOpenByChatAsync(chatId, LocalToday(), ct);
        if (row is null)
            return null;

        var counts = await store.ListUserTicketCountsAsync(row.Id, ct);
        var total = counts.Sum(item => item.TicketCount);
        var viewerCount = counts.FirstOrDefault(item => item.UserId == viewerId)?.TicketCount ?? 0;
        return new DailyInfoSnapshot(
            row,
            total,
            counts.Count,
            total * row.TicketPrice,
            viewerCount,
            counts.Take(10).ToList());
    }

    public async Task<DailySettleResult> SettleAsync(PickDailyLotteryRow row, CancellationToken ct)
    {
        var counts = await store.ListUserTicketCountsAsync(row.Id, ct);
        var tickets = counts
            .SelectMany(item => Enumerable.Repeat(
                new DailyTicketOwner(item.UserId, item.DisplayName),
                item.TicketCount))
            .ToArray();
        var command = new DailySettleCommand(
            row,
            tickets,
            $"pick:daily:settle:{row.Id:N}",
            Options.HouseFeePercent);
        return await settle.ExecuteAsync(new(command), ct);
    }

    public Task<IReadOnlyList<PickDailyLotteryRow>> HistoryAsync(
        long chatId,
        int limit,
        CancellationToken ct) =>
        store.ListHistoryAsync(chatId, Math.Clamp(limit, 1, 30), ct);
}
