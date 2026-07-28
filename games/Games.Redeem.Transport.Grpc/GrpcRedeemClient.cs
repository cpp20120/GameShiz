using System.Text.Json;
using Games.Redeem.Contracts;
using Games.Redeem.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Redeem.Transport.Grpc;

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
