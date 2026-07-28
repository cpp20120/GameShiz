using BotFramework.Host.Execution;
using Games.Pick.Application.Execution;
using Microsoft.Extensions.Options;

namespace Games.Pick.Application.Services;

public sealed class PickLotteryService(
    IPickLotteryStore store,
    IAtomicGameExecutor<QuickLotteryOpenCommand, QuickLotteryState, LotteryOpenResult> open,
    IAtomicGameExecutor<QuickLotteryJoinCommand, QuickLotteryState, LotteryJoinResult> join,
    IAtomicGameExecutor<QuickLotterySettleCommand, QuickLotteryState, LotterySettleResult> settle,
    IOptions<PickOptions> options) : IPickLotteryService
{
    private PickLotteryOptions Options => options.Value.Lottery;

    public Task<LotteryOpenResult> OpenAsync(
        long userId,
        string displayName,
        long chatId,
        int stake,
        CancellationToken ct) =>
        OpenAsync(userId, displayName, chatId, stake, 0, ct);

    public Task<LotteryOpenResult> OpenAsync(
        long userId,
        string displayName,
        long chatId,
        int stake,
        int sourceMessageId,
        CancellationToken ct)
    {
        var lotteryOptions = Options;
        var commandId = sourceMessageId != 0
            ? $"pick:lottery:open:{chatId}:{sourceMessageId}:{userId}"
            : $"pick:lottery:open:legacy:{Guid.NewGuid():N}";
        var command = new QuickLotteryOpenCommand(
            userId,
            displayName,
            chatId,
            stake,
            commandId,
            lotteryOptions.MinStake,
            lotteryOptions.MaxStake,
            lotteryOptions.DurationSeconds);
        return open.ExecuteAsync(new(command), ct);
    }

    public Task<LotteryJoinResult> JoinAsync(
        long userId,
        string displayName,
        long chatId,
        CancellationToken ct) =>
        JoinAsync(userId, displayName, chatId, 0, ct);

    public Task<LotteryJoinResult> JoinAsync(
        long userId,
        string displayName,
        long chatId,
        int sourceMessageId,
        CancellationToken ct)
    {
        var commandId = sourceMessageId != 0
            ? $"pick:lottery:join:{chatId}:{sourceMessageId}:{userId}"
            : $"pick:lottery:join:legacy:{Guid.NewGuid():N}";
        return join.ExecuteAsync(
            new(new QuickLotteryJoinCommand(userId, displayName, chatId, commandId)),
            ct);
    }

    public async Task<LotteryInfoSnapshot?> InfoAsync(long chatId, CancellationToken ct)
    {
        var row = await store.FindOpenByChatAsync(chatId, ct);
        if (row is null)
            return null;

        var entries = await store.ListEntriesAsync(row.Id, ct);
        return new LotteryInfoSnapshot(row, entries.Count, entries.Sum(entry => entry.StakePaid));
    }

    public async Task<LotterySettleResult?> CancelByOpenerAsync(
        long openerId,
        long chatId,
        CancellationToken ct)
    {
        var row = await store.FindOpenByChatAsync(chatId, ct);
        if (row is null || row.OpenerId != openerId)
            return null;

        return await ExecuteSettleAsync(row, force: true, ct);
    }

    public Task<LotterySettleResult> SettleAsync(PickLotteryRow row, CancellationToken ct) =>
        ExecuteSettleAsync(row, force: false, ct);

    private async Task<LotterySettleResult> ExecuteSettleAsync(
        PickLotteryRow row,
        bool force,
        CancellationToken ct)
    {
        var entries = await store.ListEntriesAsync(row.Id, ct);
        var lotteryOptions = Options;
        var command = new QuickLotterySettleCommand(
            row,
            entries,
            force,
            $"pick:lottery:{(force ? "cancel" : "settle")}:{row.Id:N}",
            lotteryOptions.MinEntrantsToSettle,
            lotteryOptions.HouseFeePercent);
        return await settle.ExecuteAsync(new(command), ct);
    }
}
