using BotFramework.Contracts.Messaging;
using Games.Dice.Contracts.Play;
using Games.Dice.Transport.Grpc.Wire;
using Grpc.Core;

namespace Games.Dice.Transport.Grpc;

public sealed class DiceGrpcEndpoint(IRequestClient requests) : DiceApi.DiceApiBase
{
    public override async Task<DicePlayGrpcResponse> Play(
        DicePlayGrpcRequest request,
        ServerCallContext context)
    {
        var response = await requests.SendAsync<DicePlayRequest, DicePlayResponse>(
            request.ToContract(),
            request.Metadata.ToContract(),
            context.CancellationToken);
        return response.ToGrpc();
    }
}
