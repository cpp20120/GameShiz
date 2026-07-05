using System.Text.Json;
using Games.Redeem.Contracts;
using Games.Redeem.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Redeem.Transport.Grpc;

internal static class RedeemWire
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static ContractCall Call<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static ContractReply Reply<T>(T value) => new() { PayloadJson = JsonSerializer.Serialize(value, Options) };
    public static T Read<T>(this ContractCall value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
    public static T Read<T>(this ContractReply value) => JsonSerializer.Deserialize<T>(value.PayloadJson, Options)!;
}

internal sealed record IssueCall(long UserId, string? FreeSpinGameId);
internal sealed record BeginCall(long UserId, long BalanceScopeId, string DisplayName, string CodeText);
internal sealed record VerifyCall(long UserId, Guid CodeGuid, int ChosenId);
internal sealed record CompleteCall(long UserId, long BalanceScopeId, Guid CodeGuid);

public sealed class RedeemGrpcEndpoint(IRedeemClient client) : RedeemApi.RedeemApiBase
{
    public override async Task<ContractReply> Issue(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<IssueCall>();
        return RedeemWire.Reply(await client.IssueAdminCodeAsync(x.UserId, x.FreeSpinGameId, context.CancellationToken));
    }
    public override async Task<ContractReply> Begin(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<BeginCall>();
        return RedeemWire.Reply(await client.BeginAsync(x.UserId, x.BalanceScopeId, x.DisplayName, x.CodeText, context.CancellationToken));
    }
    public override async Task<ContractReply> VerifyCaptcha(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<VerifyCall>();
        return RedeemWire.Reply(await client.VerifyCaptchaAsync(x.UserId, x.CodeGuid, x.ChosenId, context.CancellationToken));
    }
    public override async Task<ContractReply> Complete(ContractCall request, ServerCallContext context)
    {
        var x = request.Read<CompleteCall>();
        return RedeemWire.Reply(await client.CompleteAsync(x.UserId, x.BalanceScopeId, x.CodeGuid, context.CancellationToken));
    }
}

public sealed class GrpcRedeemClient(RedeemApi.RedeemApiClient client) : IRedeemClient
{
    public async Task<Guid> IssueAdminCodeAsync(long userId, string? freeSpinGameId, CancellationToken ct) =>
        (await client.IssueAsync(RedeemWire.Call(new IssueCall(userId, freeSpinGameId)), cancellationToken: ct)).Read<Guid>();
    public async Task<BeginRedeemResponse> BeginAsync(long userId, long balanceScopeId, string displayName, string codeText, CancellationToken ct) =>
        (await client.BeginAsync(RedeemWire.Call(new BeginCall(userId, balanceScopeId, displayName, codeText)), cancellationToken: ct)).Read<BeginRedeemResponse>();
    public async Task<bool> VerifyCaptchaAsync(long userId, Guid codeGuid, int chosenId, CancellationToken ct) =>
        (await client.VerifyCaptchaAsync(RedeemWire.Call(new VerifyCall(userId, codeGuid, chosenId)), cancellationToken: ct)).Read<bool>();
    public async Task<CompleteRedeemResponse> CompleteAsync(long userId, long balanceScopeId, Guid codeGuid, CancellationToken ct) =>
        (await client.CompleteAsync(RedeemWire.Call(new CompleteCall(userId, balanceScopeId, codeGuid)), cancellationToken: ct)).Read<CompleteRedeemResponse>();
}
