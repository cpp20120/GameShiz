namespace Games.Pick.Application.Services;

public sealed record PickDailySchedule(int OffsetHours, int DrawHourLocal);

public interface IPickClient
{
    Task<PickResult> PickAsync(long userId, string displayName, long chatId, int amount,
        IReadOnlyList<string> variants, IReadOnlyList<int> backedIndices, CancellationToken ct);
    Task<PickResult> ContinueChainAsync(PickChainState chain, CancellationToken ct);
    Task<PickChainState?> ClaimChainAsync(Guid chainId, CancellationToken ct);
    Task RestoreChainAsync(PickChainState chain, CancellationToken ct);

    Task<LotteryOpenResult> OpenLotteryAsync(long userId, string displayName, long chatId, int stake, CancellationToken ct);
    Task<LotteryJoinResult> JoinLotteryAsync(long userId, string displayName, long chatId, CancellationToken ct);
    Task<LotteryInfoSnapshot?> LotteryInfoAsync(long chatId, CancellationToken ct);
    Task<LotterySettleResult?> CancelLotteryAsync(long openerId, long chatId, CancellationToken ct);

    Task<DailyBuyResult> BuyDailyAsync(long userId, string displayName, long chatId, int count, CancellationToken ct);
    Task<DailyInfoSnapshot?> DailyInfoAsync(long chatId, long viewerId, CancellationToken ct);
    Task<IReadOnlyList<PickDailyLotteryRow>> DailyHistoryAsync(long chatId, int limit, CancellationToken ct);
    Task<PickDailySchedule> GetDailyScheduleAsync(CancellationToken ct);
}
