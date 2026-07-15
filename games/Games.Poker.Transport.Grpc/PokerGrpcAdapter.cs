using System.Text.Json;
using Games.Poker.Application.Services;
using Games.Poker.Domain.Entities;
using Games.Poker.Domain.Results;
using Games.Poker.Transport.Grpc.Wire;
using Grpc.Core;
using PokerActionResult = Games.Poker.Domain.Results.ActionResult;

namespace Games.Poker.Transport.Grpc;

internal static class PokerWire
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static ContractCall Call<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static ContractReply Reply<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static T Read<T>(this ContractCall value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
    public static T Read<T>(this ContractReply value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
}

internal sealed record PokerUserCall(long UserId, long ChatId = 0, string OperationId = "");
internal sealed record PokerTableCall(long UserId, string DisplayName, long ChatId, string Code = "", string OperationId = "");
internal sealed record PokerActionCall(long UserId, long ChatId, string Verb, int Amount, string OperationId = "");
internal sealed record PokerCodeCall(string Code, int MessageId = 0);
internal sealed record PokerCutoffCall(long CutoffMs);
internal sealed record PokerTableReply(TableSnapshot? Snapshot, PokerSeat? Seat);
internal sealed record PokerEmptyReply;

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
        return PokerWire.Reply(new PokerEmptyReply());
    }

    public override async Task<ContractReply> ListStuckCodes(ContractCall request, ServerCallContext context) =>
        PokerWire.Reply(await service.ListStuckCodesAsync(request.Read<PokerCutoffCall>().CutoffMs, context.CancellationToken));
}

public sealed class GrpcPokerService(PokerApi.PokerApiClient client) : IPokerService
{
    public async Task<(TableSnapshot? Snapshot, PokerSeat? MySeat)> FindMyTableAsync(long userId, long currentChatId, CancellationToken ct)
    {
        var reply = (await client.FindMyTableAsync(PokerWire.Call(new PokerUserCall(userId, currentChatId)), cancellationToken: ct)).Read<PokerTableReply>();
        return (reply.Snapshot, reply.Seat);
    }

    public Task<CreateResult> CreateTableAsync(long userId, string displayName, long chatId, CancellationToken ct) =>
        CreateTableCoreAsync(userId, displayName, chatId, ct);
    public Task<CreateResult> CreateTableAsync(long userId, string displayName, long chatId, int sourceMessageId, CancellationToken ct) =>
        CreateTableCoreAsync(userId, displayName, chatId, ct);
    public Task<CreateResult> CreateTableAsync(long userId, string displayName, long chatId, string operationId, CancellationToken ct) =>
        CreateTableCoreAsync(userId, displayName, chatId, operationId, ct);
    private async Task<CreateResult> CreateTableCoreAsync(long userId, string displayName, long chatId, CancellationToken ct) =>
        (await client.CreateTableAsync(PokerWire.Call(new PokerTableCall(userId, displayName, chatId)), cancellationToken: ct)).Read<CreateResult>();
    private async Task<CreateResult> CreateTableCoreAsync(long userId, string displayName, long chatId, string operationId, CancellationToken ct) =>
        (await client.CreateTableAsync(PokerWire.Call(new PokerTableCall(userId, displayName, chatId, OperationId: operationId)), cancellationToken: ct)).Read<CreateResult>();

    public Task<JoinResult> JoinTableAsync(long userId, string displayName, long chatId, string code, CancellationToken ct) =>
        JoinTableCoreAsync(userId, displayName, chatId, code, ct);
    public Task<JoinResult> JoinTableAsync(long userId, string displayName, long chatId, string code, int sourceMessageId, CancellationToken ct) =>
        JoinTableCoreAsync(userId, displayName, chatId, code, ct);
    public Task<JoinResult> JoinTableAsync(long userId, string displayName, long chatId, string code, string operationId, CancellationToken ct) =>
        JoinTableCoreAsync(userId, displayName, chatId, code, operationId, ct);
    private async Task<JoinResult> JoinTableCoreAsync(long userId, string displayName, long chatId, string code, CancellationToken ct) =>
        (await client.JoinTableAsync(PokerWire.Call(new PokerTableCall(userId, displayName, chatId, code)), cancellationToken: ct)).Read<JoinResult>();
    private async Task<JoinResult> JoinTableCoreAsync(long userId, string displayName, long chatId, string code, string operationId, CancellationToken ct) =>
        (await client.JoinTableAsync(PokerWire.Call(new PokerTableCall(userId, displayName, chatId, code, operationId)), cancellationToken: ct)).Read<JoinResult>();

    public async Task<StartResult> StartHandAsync(long userId, long currentChatId, CancellationToken ct) =>
        (await client.StartHandAsync(PokerWire.Call(new PokerUserCall(userId, currentChatId)), cancellationToken: ct)).Read<StartResult>();
    public async Task<StartResult> StartHandAsync(long userId, long currentChatId, string operationId, CancellationToken ct) =>
        (await client.StartHandAsync(PokerWire.Call(new PokerUserCall(userId, currentChatId, operationId)), cancellationToken: ct)).Read<StartResult>();
    public async Task<PokerActionResult> ApplyPlayerActionAsync(long userId, long currentChatId, string verb, int amount, CancellationToken ct) =>
        (await client.ApplyActionAsync(PokerWire.Call(new PokerActionCall(userId, currentChatId, verb, amount)), cancellationToken: ct)).Read<PokerActionResult>();
    public async Task<PokerActionResult> ApplyPlayerActionAsync(long userId, long currentChatId, string verb, int amount, string operationId, CancellationToken ct) =>
        (await client.ApplyActionAsync(PokerWire.Call(new PokerActionCall(userId, currentChatId, verb, amount, operationId)), cancellationToken: ct)).Read<PokerActionResult>();
    public async Task<PokerActionResult?> RunAutoActionAsync(string inviteCode, CancellationToken ct) =>
        (await client.RunAutoActionAsync(PokerWire.Call(new PokerCodeCall(inviteCode)), cancellationToken: ct)).Read<PokerActionResult?>();
    public async Task<LeaveResult> LeaveTableAsync(long userId, long currentChatId, CancellationToken ct) =>
        (await client.LeaveTableAsync(PokerWire.Call(new PokerUserCall(userId, currentChatId)), cancellationToken: ct)).Read<LeaveResult>();
    public async Task<LeaveResult> LeaveTableAsync(long userId, long currentChatId, string operationId, CancellationToken ct) =>
        (await client.LeaveTableAsync(PokerWire.Call(new PokerUserCall(userId, currentChatId, operationId)), cancellationToken: ct)).Read<LeaveResult>();
    public async Task SetTableStateMessageIdAsync(string inviteCode, int messageId, CancellationToken ct) =>
        _ = await client.SetStateMessageAsync(PokerWire.Call(new PokerCodeCall(inviteCode, messageId)), cancellationToken: ct);
    public async Task<IReadOnlyList<string>> ListStuckCodesAsync(long cutoffMs, CancellationToken ct) =>
        (await client.ListStuckCodesAsync(PokerWire.Call(new PokerCutoffCall(cutoffMs)), cancellationToken: ct)).Read<List<string>>();
}
