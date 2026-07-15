using Games.Basketball.Application.Services;
using Games.Bowling.Application.Services;
using Games.Darts.Application.Services;
using Games.DiceCube.Application.Services;
using Games.Football.Application.Services;
using Games.NativeDice.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.NativeDice.Transport.Grpc;

public sealed class NativeDiceGrpcEndpoint(IServiceProvider services) : NativeDiceApi.NativeDiceApiBase
{
    private T Service<T>() where T : class =>
        (services.GetService(typeof(T)) as T) ?? throw new RpcException(new Status(
            StatusCode.Unimplemented,
            $"The backend does not own the '{typeof(T).Name}' game."));

    public override async Task<ContractReply> DiceCubePlaceBet(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<BetCall>();
        return NativeDiceWireCodec.Reply(await Service<IDiceCubeService>().PlaceBetAsync(x.UserId, x.DisplayName, x.ChatId, x.Amount, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> DiceCubeRoll(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<RollCall>();
        return NativeDiceWireCodec.Reply(await Service<IDiceCubeService>().RollAsync(x.UserId, x.DisplayName, x.ChatId, x.Face, context.CancellationToken));
    }

    public override async Task<ContractReply> DiceCubeAbort(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<AbortCall>();
        await Service<IDiceCubeService>().AbortPendingBetAfterSendDiceFailedAsync(x.UserId, x.ChatId, context.CancellationToken);
        return NativeDiceWireCodec.Reply(new EmptyReply());
    }

    public override async Task<ContractReply> DartsPlaceBet(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<BetCall>();
        return NativeDiceWireCodec.Reply(await Service<IDartsService>().PlaceBetAsync(x.UserId, x.DisplayName, x.ChatId, x.Amount, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> DartsThrow(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<DartsThrowCall>();
        return NativeDiceWireCodec.Reply(await Service<IDartsService>().ThrowAsync(x.RoundId, x.UserId, x.DisplayName, x.ChatId, x.MessageId, x.Face, context.CancellationToken));
    }

    public override async Task<ContractReply> DartsQuickThrow(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<DartsThrowCall>();
        return NativeDiceWireCodec.Reply(await Service<IDartsService>().QuickThrowAsync(x.UserId, x.DisplayName, x.ChatId, x.MessageId, x.Face, x.Amount, context.CancellationToken));
    }

    public override async Task<ContractReply> DartsAbort(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<DartsAbortCall>();
        await Service<IDartsService>().AbortQueuedRoundIfBetReplyFailedAsync(x.RoundId, x.UserId, x.ChatId, context.CancellationToken);
        return NativeDiceWireCodec.Reply(new EmptyReply());
    }

    public override async Task<ContractReply> FootballPlaceBet(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<BetCall>();
        return NativeDiceWireCodec.Reply(await Service<IFootballService>().PlaceBetAsync(x.UserId, x.DisplayName, x.ChatId, x.Amount, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> FootballThrow(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<RollCall>();
        return NativeDiceWireCodec.Reply(await Service<IFootballService>().ThrowAsync(
            x.UserId, x.DisplayName, x.ChatId, x.Face, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> FootballAbort(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<AbortCall>();
        await Service<IFootballService>().AbortPendingBetAfterSendDiceFailedAsync(
            x.UserId,
            string.IsNullOrWhiteSpace(x.DisplayName) ? $"User ID: {x.UserId}" : x.DisplayName,
            x.ChatId,
            x.SourceMessageId,
            context.CancellationToken);
        return NativeDiceWireCodec.Reply(new EmptyReply());
    }

    public override async Task<ContractReply> BasketballPlaceBet(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<BetCall>();
        return NativeDiceWireCodec.Reply(await Service<IBasketballService>().PlaceBetAsync(x.UserId, x.DisplayName, x.ChatId, x.Amount, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> BasketballThrow(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<RollCall>();
        return NativeDiceWireCodec.Reply(await Service<IBasketballService>().ThrowAsync(
            x.UserId,
            x.DisplayName,
            x.ChatId,
            x.Face,
            x.SourceMessageId,
            context.CancellationToken));
    }

    public override async Task<ContractReply> BasketballAbort(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<AbortCall>();
        await Service<IBasketballService>().AbortPendingBetAfterSendDiceFailedAsync(
            x.UserId,
            string.IsNullOrWhiteSpace(x.DisplayName) ? $"User ID: {x.UserId}" : x.DisplayName,
            x.ChatId,
            x.SourceMessageId,
            context.CancellationToken);
        return NativeDiceWireCodec.Reply(new EmptyReply());
    }

    public override async Task<ContractReply> BowlingPlaceBet(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<BetCall>();
        return NativeDiceWireCodec.Reply(await Service<IBowlingService>().PlaceBetAsync(x.UserId, x.DisplayName, x.ChatId, x.Amount, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> BowlingRoll(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<RollCall>();
        return NativeDiceWireCodec.Reply(await Service<IBowlingService>().RollAsync(
            x.UserId, x.DisplayName, x.ChatId, x.Face, x.SourceMessageId, context.CancellationToken));
    }

    public override async Task<ContractReply> BowlingAbort(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<AbortCall>();
        await Service<IBowlingService>().AbortPendingBetAfterSendDiceFailedAsync(
            x.UserId,
            string.IsNullOrWhiteSpace(x.DisplayName) ? $"User ID: {x.UserId}" : x.DisplayName,
            x.ChatId,
            x.SourceMessageId,
            context.CancellationToken);
        return NativeDiceWireCodec.Reply(new EmptyReply());
    }
}
