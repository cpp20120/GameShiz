using System.Text.Json;
using Games.Redeem.Contracts;
using Games.Redeem.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Redeem.Transport.Grpc;

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
