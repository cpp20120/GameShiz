using Games.Basketball.Application.Services;
using Games.Bowling.Application.Services;
using Games.Darts.Application.Services;
using Games.DiceCube.Application.Services;
using Games.Football.Application.Services;
using Games.NativeDice.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.NativeDice.Transport.Grpc;

public sealed class NativeDiceGrpcEndpoint(
    IDiceCubeService cube,
    IDartsService darts,
    IFootballService football,
    IBasketballService basketball,
    IBowlingService bowling) : NativeDiceApi.NativeDiceApiBase
{
    public override async Task<ContractReply> DiceCubePlaceBet(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<BetCall>();
        return NativeDiceWireCodec.Reply(await cube.PlaceBetAsync(x.UserId, x.DisplayName, x.ChatId, x.Amount, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> DiceCubeRoll(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<RollCall>();
        return NativeDiceWireCodec.Reply(await cube.RollAsync(x.UserId, x.DisplayName, x.ChatId, x.Face, context.CancellationToken));
    }

    public override async Task<ContractReply> DiceCubeAbort(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<AbortCall>();
        await cube.AbortPendingBetAfterSendDiceFailedAsync(x.UserId, x.ChatId, context.CancellationToken);
        return NativeDiceWireCodec.Reply(new EmptyReply());
    }

    public override async Task<ContractReply> DartsPlaceBet(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<BetCall>();
        return NativeDiceWireCodec.Reply(await darts.PlaceBetAsync(x.UserId, x.DisplayName, x.ChatId, x.Amount, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> DartsThrow(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<DartsThrowCall>();
        return NativeDiceWireCodec.Reply(await darts.ThrowAsync(x.RoundId, x.UserId, x.DisplayName, x.ChatId, x.MessageId, x.Face, context.CancellationToken));
    }

    public override async Task<ContractReply> DartsQuickThrow(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<DartsThrowCall>();
        return NativeDiceWireCodec.Reply(await darts.QuickThrowAsync(x.UserId, x.DisplayName, x.ChatId, x.MessageId, x.Face, x.Amount, context.CancellationToken));
    }

    public override async Task<ContractReply> DartsAbort(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<DartsAbortCall>();
        await darts.AbortQueuedRoundIfBetReplyFailedAsync(x.RoundId, x.UserId, x.ChatId, context.CancellationToken);
        return NativeDiceWireCodec.Reply(new EmptyReply());
    }

    public override async Task<ContractReply> FootballPlaceBet(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<BetCall>();
        return NativeDiceWireCodec.Reply(await football.PlaceBetAsync(x.UserId, x.DisplayName, x.ChatId, x.Amount, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> FootballThrow(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<RollCall>();
        return NativeDiceWireCodec.Reply(await football.ThrowAsync(x.UserId, x.DisplayName, x.ChatId, x.Face, context.CancellationToken));
    }

    public override async Task<ContractReply> FootballAbort(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<AbortCall>();
        await football.AbortPendingBetAfterSendDiceFailedAsync(x.UserId, x.ChatId, context.CancellationToken);
        return NativeDiceWireCodec.Reply(new EmptyReply());
    }

    public override async Task<ContractReply> BasketballPlaceBet(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<BetCall>();
        return NativeDiceWireCodec.Reply(await basketball.PlaceBetAsync(x.UserId, x.DisplayName, x.ChatId, x.Amount, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> BasketballThrow(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<RollCall>();
        return NativeDiceWireCodec.Reply(await basketball.ThrowAsync(x.UserId, x.DisplayName, x.ChatId, x.Face, context.CancellationToken));
    }

    public override async Task<ContractReply> BasketballAbort(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<AbortCall>();
        await basketball.AbortPendingBetAfterSendDiceFailedAsync(x.UserId, x.ChatId, context.CancellationToken);
        return NativeDiceWireCodec.Reply(new EmptyReply());
    }

    public override async Task<ContractReply> BowlingPlaceBet(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<BetCall>();
        return NativeDiceWireCodec.Reply(await bowling.PlaceBetAsync(x.UserId, x.DisplayName, x.ChatId, x.Amount, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> BowlingRoll(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<RollCall>();
        return NativeDiceWireCodec.Reply(await bowling.RollAsync(x.UserId, x.DisplayName, x.ChatId, x.Face, context.CancellationToken));
    }

    public override async Task<ContractReply> BowlingAbort(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<AbortCall>();
        await bowling.AbortPendingBetAfterSendDiceFailedAsync(x.UserId, x.ChatId, context.CancellationToken);
        return NativeDiceWireCodec.Reply(new EmptyReply());
    }
}
