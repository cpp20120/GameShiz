using System.Text.Json;
using Games.Poker.Application.Services;
using Games.Poker.Domain.Entities;
using Games.Poker.Domain.Results;
using Games.Poker.Transport.Grpc.Wire;
using Grpc.Core;
using PokerActionResult = Games.Poker.Domain.Results.ActionResult;

namespace Games.Poker.Transport.Grpc;

public sealed class PokerGrpcEndpoint(IPokerService service) : PokerApi.PokerApiBase
{
    public override async Task<ContractReply> FindMyTable(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<PokerUserCall>();
        var (snapshot, seat) = await service.FindMyTableAsync(call.UserId, call.ChatId, context.CancellationToken);
        return PokerWire.Reply(new PokerTableReply(snapshot, seat));
    }

    public override async Task<ContractReply> CreateTable(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<PokerTableCall>();
        return PokerWire.Reply(string.IsNullOrWhiteSpace(call.OperationId)
            ? await service.CreateTableAsync(call.UserId, call.DisplayName, call.ChatId, context.CancellationToken)
            : await service.CreateTableAsync(call.UserId, call.DisplayName, call.ChatId, call.OperationId, context.CancellationToken));
    }

    public override async Task<ContractReply> JoinTable(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<PokerTableCall>();
        return PokerWire.Reply(string.IsNullOrWhiteSpace(call.OperationId)
            ? await service.JoinTableAsync(call.UserId, call.DisplayName, call.ChatId, call.Code, context.CancellationToken)
            : await service.JoinTableAsync(call.UserId, call.DisplayName, call.ChatId, call.Code, call.OperationId, context.CancellationToken));
    }

    public override async Task<ContractReply> StartHand(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<PokerUserCall>();
        return PokerWire.Reply(string.IsNullOrWhiteSpace(call.OperationId)
            ? await service.StartHandAsync(call.UserId, call.ChatId, context.CancellationToken)
            : await service.StartHandAsync(call.UserId, call.ChatId, call.OperationId, context.CancellationToken));
    }

    public override async Task<ContractReply> ApplyAction(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<PokerActionCall>();
        return PokerWire.Reply(string.IsNullOrWhiteSpace(call.OperationId)
            ? await service.ApplyPlayerActionAsync(call.UserId, call.ChatId, call.Verb, call.Amount, context.CancellationToken)
            : await service.ApplyPlayerActionAsync(call.UserId, call.ChatId, call.Verb, call.Amount, call.OperationId, context.CancellationToken));
    }

    public override async Task<ContractReply> RunAutoAction(ContractCall request, ServerCallContext context) =>
        PokerWire.Reply(await service.RunAutoActionAsync(request.Read<PokerCodeCall>().Code, context.CancellationToken));

    public override async Task<ContractReply> LeaveTable(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<PokerUserCall>();
        return PokerWire.Reply(string.IsNullOrWhiteSpace(call.OperationId)
            ? await service.LeaveTableAsync(call.UserId, call.ChatId, context.CancellationToken)
            : await service.LeaveTableAsync(call.UserId, call.ChatId, call.OperationId, context.CancellationToken));
    }

    public override async Task<ContractReply> SetStateMessage(ContractCall request, ServerCallContext context)
    {
        var call = request.Read<PokerCodeCall>();
        await service.SetTableStateMessageIdAsync(call.Code, call.MessageId, context.CancellationToken);
        return PokerWire.Reply(PokerEmptyReply.Create());
    }

    public override async Task<ContractReply> ListStuckCodes(ContractCall request, ServerCallContext context) =>
        PokerWire.Reply(await service.ListStuckCodesAsync(request.Read<PokerCutoffCall>().CutoffMs, context.CancellationToken));
}
