using Games.Pick.Application.Services;
using Games.Pick.Transport.Grpc.Wire;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Games.Pick.Transport.Grpc;

public sealed class PickGrpcEndpoint(IPickClient client) : PickApi.PickApiBase
{
    public override async Task<ContractReply> Pick(
        ContractCall request,
        ServerCallContext context)
    {
        var pick = request.Read<PickCall>();
        return PickWire.Reply(await client.PickAsync(
            pick.UserId,
            pick.Name,
            pick.ChatId,
            pick.Amount,
            pick.Variants,
            pick.Backed,
            pick.SourceMessageId,
            context.CancellationToken));
    }

    public override async Task<ContractReply> ContinueChain(
        ContractCall request,
        ServerCallContext context) =>
        PickWire.Reply(await client.ContinueChainAsync(
            request.Read<ChainCall>().Chain,
            context.CancellationToken));

    public override async Task<ContractReply> ClaimChain(
        ContractCall request,
        ServerCallContext context) =>
        PickWire.Reply(await client.ClaimChainAsync(
            request.Read<ChainIdCall>().Id,
            context.CancellationToken));

    public override async Task<ContractReply> RestoreChain(
        ContractCall request,
        ServerCallContext context)
    {
        await client.RestoreChainAsync(request.Read<ChainCall>().Chain, context.CancellationToken);
        return PickWire.Reply(EmptyReply.Create());
    }

    public override async Task<ContractReply> OpenLottery(
        ContractCall request,
        ServerCallContext context)
    {
        var user = request.Read<UserCall>();
        return PickWire.Reply(await client.OpenLotteryAsync(
            user.UserId,
            user.Name,
            user.ChatId,
            user.Value,
            user.SourceMessageId,
            context.CancellationToken));
    }

    public override async Task<ContractReply> JoinLottery(
        ContractCall request,
        ServerCallContext context)
    {
        var user = request.Read<UserCall>();
        return PickWire.Reply(await client.JoinLotteryAsync(
            user.UserId,
            user.Name,
            user.ChatId,
            user.SourceMessageId,
            context.CancellationToken));
    }

    public override async Task<ContractReply> LotteryInfo(
        ContractCall request,
        ServerCallContext context) =>
        PickWire.Reply(await client.LotteryInfoAsync(
            request.Read<ChatCall>().ChatId,
            context.CancellationToken));

    public override async Task<ContractReply> CancelLottery(
        ContractCall request,
        ServerCallContext context)
    {
        var chat = request.Read<ChatCall>();
        return PickWire.Reply(await client.CancelLotteryAsync(
            chat.UserId,
            chat.ChatId,
            context.CancellationToken));
    }

    public override async Task<ContractReply> BuyDaily(
        ContractCall request,
        ServerCallContext context)
    {
        var user = request.Read<UserCall>();
        return PickWire.Reply(await client.BuyDailyAsync(
            user.UserId,
            user.Name,
            user.ChatId,
            user.Value,
            user.SourceMessageId,
            context.CancellationToken));
    }

    public override async Task<ContractReply> DailyInfo(
        ContractCall request,
        ServerCallContext context)
    {
        var chat = request.Read<ChatCall>();
        return PickWire.Reply(await client.DailyInfoAsync(
            chat.ChatId,
            chat.UserId,
            context.CancellationToken));
    }

    public override async Task<ContractReply> DailyHistory(
        ContractCall request,
        ServerCallContext context)
    {
        var chat = request.Read<ChatCall>();
        return PickWire.Reply(await client.DailyHistoryAsync(
            chat.ChatId,
            chat.Limit,
            context.CancellationToken));
    }

    public override async Task<ContractReply> DailySchedule(
        ContractCall request,
        ServerCallContext context) =>
        PickWire.Reply(await client.GetDailyScheduleAsync(context.CancellationToken));

    public override async Task<DailyScheduleReply> DailyScheduleTyped(
        Empty request,
        ServerCallContext context)
    {
        var schedule = await client.GetDailyScheduleAsync(context.CancellationToken);
        return new DailyScheduleReply
        {
            OffsetHours = schedule.OffsetHours,
            DrawHourLocal = schedule.DrawHourLocal,
        };
    }
}
