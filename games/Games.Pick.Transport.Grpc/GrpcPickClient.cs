using Games.Pick.Application.Services;
using Games.Pick.Application.Results;
using Games.Pick.Application.Analytics;
using Games.Pick.Domain.Results;
using Games.Pick.Infrastructure.Persistence;
using Games.Pick.Transport.Grpc.Wire;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Games.Pick.Transport.Grpc;

public sealed class GrpcPickClient(PickApi.PickApiClient client) : IPickClient
{
    public async Task<PickResult> PickAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        IReadOnlyList<string> variants,
        IReadOnlyList<int> backedIndices,
        CancellationToken ct) =>
        (await client.PickAsync(
            PickWire.Call(new PickCall(userId, displayName, chatId, amount, variants, backedIndices)),
            cancellationToken: ct)).Read<PickResult>();

    public async Task<PickResult> PickAsync(
        long userId,
        string displayName,
        long chatId,
        int amount,
        IReadOnlyList<string> variants,
        IReadOnlyList<int> backedIndices,
        int sourceMessageId,
        CancellationToken ct) =>
        (await client.PickAsync(
            PickWire.Call(new PickCall(userId, displayName, chatId, amount, variants, backedIndices, sourceMessageId)),
            cancellationToken: ct)).Read<PickResult>();

    public async Task<PickResult> ContinueChainAsync(PickChainState chain, CancellationToken ct) =>
        (await client.ContinueChainAsync(
            PickWire.Call(new ChainCall(chain)),
            cancellationToken: ct)).Read<PickResult>();

    public async Task<PickChainState?> ClaimChainAsync(Guid chainId, CancellationToken ct) =>
        (await client.ClaimChainAsync(
            PickWire.Call(new ChainIdCall(chainId)),
            cancellationToken: ct)).Read<PickChainState?>();

    public async Task RestoreChainAsync(PickChainState chain, CancellationToken ct) =>
        _ = await client.RestoreChainAsync(
            PickWire.Call(new ChainCall(chain)),
            cancellationToken: ct);

    public async Task<LotteryOpenResult> OpenLotteryAsync(
        long userId,
        string displayName,
        long chatId,
        int stake,
        CancellationToken ct) =>
        (await client.OpenLotteryAsync(
            PickWire.Call(new UserCall(userId, displayName, chatId, stake)),
            cancellationToken: ct)).Read<LotteryOpenResult>();

    public async Task<LotteryOpenResult> OpenLotteryAsync(
        long userId,
        string displayName,
        long chatId,
        int stake,
        int sourceMessageId,
        CancellationToken ct) =>
        (await client.OpenLotteryAsync(
            PickWire.Call(new UserCall(userId, displayName, chatId, stake, sourceMessageId)),
            cancellationToken: ct)).Read<LotteryOpenResult>();

    public async Task<LotteryJoinResult> JoinLotteryAsync(
        long userId,
        string displayName,
        long chatId,
        CancellationToken ct) =>
        (await client.JoinLotteryAsync(
            PickWire.Call(new UserCall(userId, displayName, chatId)),
            cancellationToken: ct)).Read<LotteryJoinResult>();

    public async Task<LotteryJoinResult> JoinLotteryAsync(
        long userId,
        string displayName,
        long chatId,
        int sourceMessageId,
        CancellationToken ct) =>
        (await client.JoinLotteryAsync(
            PickWire.Call(new UserCall(userId, displayName, chatId, SourceMessageId: sourceMessageId)),
            cancellationToken: ct)).Read<LotteryJoinResult>();

    public async Task<LotteryInfoSnapshot?> LotteryInfoAsync(long chatId, CancellationToken ct) =>
        (await client.LotteryInfoAsync(
            PickWire.Call(new ChatCall(chatId)),
            cancellationToken: ct)).Read<LotteryInfoSnapshot?>();

    public async Task<LotterySettleResult?> CancelLotteryAsync(long openerId, long chatId, CancellationToken ct) =>
        (await client.CancelLotteryAsync(
            PickWire.Call(new ChatCall(chatId, openerId)),
            cancellationToken: ct)).Read<LotterySettleResult?>();

    public async Task<DailyBuyResult> BuyDailyAsync(
        long userId,
        string displayName,
        long chatId,
        int count,
        CancellationToken ct) =>
        (await client.BuyDailyAsync(
            PickWire.Call(new UserCall(userId, displayName, chatId, count)),
            cancellationToken: ct)).Read<DailyBuyResult>();

    public async Task<DailyBuyResult> BuyDailyAsync(
        long userId,
        string displayName,
        long chatId,
        int count,
        int sourceMessageId,
        CancellationToken ct) =>
        (await client.BuyDailyAsync(
            PickWire.Call(new UserCall(userId, displayName, chatId, count, sourceMessageId)),
            cancellationToken: ct)).Read<DailyBuyResult>();

    public async Task<DailyInfoSnapshot?> DailyInfoAsync(long chatId, long viewerId, CancellationToken ct) =>
        (await client.DailyInfoAsync(
            PickWire.Call(new ChatCall(chatId, viewerId)),
            cancellationToken: ct)).Read<DailyInfoSnapshot?>();

    public async Task<IReadOnlyList<PickDailyLotteryRow>> DailyHistoryAsync(
        long chatId,
        int limit,
        CancellationToken ct) =>
        (await client.DailyHistoryAsync(
            PickWire.Call(new ChatCall(chatId, Limit: limit)),
            cancellationToken: ct)).Read<IReadOnlyList<PickDailyLotteryRow>>();

    public async Task<PickDailySchedule> GetDailyScheduleAsync(CancellationToken ct)
    {
        try
        {
            var schedule = await client.DailyScheduleTypedAsync(
                new Empty(),
                cancellationToken: ct);
            return new PickDailySchedule(schedule.OffsetHours, schedule.DrawHourLocal);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.Unimplemented)
        {
            return (await client.DailyScheduleAsync(
                PickWire.Call(EmptyCall.Create()),
                cancellationToken: ct)).Read<PickDailySchedule>();
        }
    }
}
