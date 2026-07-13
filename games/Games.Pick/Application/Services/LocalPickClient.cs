namespace Games.Pick.Application.Services;

public sealed class LocalPickClient(
    IPickService pick,
    IPickLotteryService lottery,
    IPickDailyLotteryService daily) : IPickClient
{
    public Task<PickResult> PickAsync(long userId, string displayName, long chatId, int amount, IReadOnlyList<string> variants, IReadOnlyList<int> backedIndices, CancellationToken ct) => pick.PickAsync(userId, displayName, chatId, amount, variants, backedIndices, ct);
    public Task<PickResult> PickAsync(long userId, string displayName, long chatId, int amount, IReadOnlyList<string> variants, IReadOnlyList<int> backedIndices, int sourceMessageId, CancellationToken ct) => pick.PickAsync(userId, displayName, chatId, amount, variants, backedIndices, sourceMessageId, ct);
    public Task<PickResult> ContinueChainAsync(PickChainState chain, CancellationToken ct) => pick.ContinueChainAsync(chain, ct);
    public Task<PickChainState?> ClaimChainAsync(Guid chainId, CancellationToken ct) => pick.ClaimChainAsync(chainId, ct);
    public Task RestoreChainAsync(PickChainState chain, CancellationToken ct) => pick.RestoreChainAsync(chain, ct);
    public Task<LotteryOpenResult> OpenLotteryAsync(long userId, string displayName, long chatId, int stake, CancellationToken ct) => lottery.OpenAsync(userId, displayName, chatId, stake, ct);
    public Task<LotteryOpenResult> OpenLotteryAsync(long userId, string displayName, long chatId, int stake, int sourceMessageId, CancellationToken ct) => lottery.OpenAsync(userId, displayName, chatId, stake, sourceMessageId, ct);
    public Task<LotteryJoinResult> JoinLotteryAsync(long userId, string displayName, long chatId, CancellationToken ct) => lottery.JoinAsync(userId, displayName, chatId, ct);
    public Task<LotteryJoinResult> JoinLotteryAsync(long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct) => lottery.JoinAsync(userId, displayName, chatId, sourceMessageId, ct);
    public Task<LotteryInfoSnapshot?> LotteryInfoAsync(long chatId, CancellationToken ct) => lottery.InfoAsync(chatId, ct);
    public Task<LotterySettleResult?> CancelLotteryAsync(long openerId, long chatId, CancellationToken ct) => lottery.CancelByOpenerAsync(openerId, chatId, ct);
    public Task<DailyBuyResult> BuyDailyAsync(long userId, string displayName, long chatId, int count, CancellationToken ct) => daily.BuyAsync(userId, displayName, chatId, count, ct);
    public Task<DailyBuyResult> BuyDailyAsync(long userId, string displayName, long chatId, int count, int sourceMessageId, CancellationToken ct) => daily.BuyAsync(userId, displayName, chatId, count, sourceMessageId, ct);
    public Task<DailyInfoSnapshot?> DailyInfoAsync(long chatId, long viewerId, CancellationToken ct) => daily.InfoAsync(chatId, viewerId, ct);
    public Task<IReadOnlyList<PickDailyLotteryRow>> DailyHistoryAsync(long chatId, int limit, CancellationToken ct) => daily.HistoryAsync(chatId, limit, ct);
    public Task<PickDailySchedule> GetDailyScheduleAsync(CancellationToken ct) => Task.FromResult(new PickDailySchedule(daily.OffsetHours, daily.DrawHourLocal));
}
